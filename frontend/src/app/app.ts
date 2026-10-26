
import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';

interface SpectrumPoint {
  id: number;
  measurementId: number;
  wavelength: number;
  intensity: number;
}

interface Measurement {
  id: number;
  name: string;
  createdAtUtc: string;
  points: SpectrumPoint[];
}


interface SpectrumAnalysis {
  peakWavelengthNm: number;
  peakIntensity: number;
  integratedIntensity: number;
  pointCount: number;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  readonly uploading = signal(false);
  readonly uploadMessage = signal('');
  readonly analyses = signal<Record<number, SpectrumAnalysis>>({});
  readonly analysisErrors = signal<Record<number, string>>({});

  loadAnalysis(measurementId: number): void {
    this.http.get<SpectrumAnalysis>(
      `http://localhost:5071/api/measurements/${measurementId}/analysis`
    ).subscribe({
      next: result => {
        this.analyses.update(current => ({
          ...current,
          [measurementId]: result
        }));
      },
      error: () => {
        this.analysisErrors.update(current => ({
          ...current,
          [measurementId]: 'Unable to analyse this measurement.'
        }));
      }
    });
  }


  async uploadCsv(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) return;

    this.uploading.set(true);
    this.uploadMessage.set('');

    try {
      const text = await file.text();
      const lines = text.replace(/^\uFEFF/, '')
        .trim()
        .split(/\r?\n/);

      const headers = lines[0]
        .split(',')
        .map(value => value.trim().toLowerCase());

      if (
        headers.length !== 2 ||
        headers[0] !== 'wavelength' ||
        headers[1] !== 'intensity'
      ) {
        throw new Error(
          'CSV must have wavelength,intensity columns.'
        );
      }

      const points = lines.slice(1).map(line => {
        const columns = line.split(',').map(value => value.trim());

        if (columns.length !== 2 || columns.some(v => v === '')) {
          throw new Error('Invalid CSV row.');
        }

        const wavelength = Number(columns[0]);
        const intensity = Number(columns[1]);

        if (
          !Number.isFinite(wavelength) ||
          !Number.isFinite(intensity) ||
          wavelength <= 0
        ) {
          throw new Error('Invalid measurement values.');
        }

        return { wavelength, intensity };
      });

      if (points.length < 2 || points.length > 10000) {
        throw new Error('CSV must contain 2–10000 data points.');
      }

      const name = file.name.replace(/\.csv$/i, '').trim();

      if (!name || name.length > 100) {
        throw new Error('Invalid measurement name.');
      }

      this.http.post<Measurement>(
        'http://localhost:5071/api/measurements',
        { name, points }
      ).subscribe({
        next: measurement => {
          this.measurements.update(items => [...items, measurement]);
          this.loadAnalysis(measurement.id);
          this.uploadMessage.set('Measurement uploaded successfully.');
          this.uploading.set(false);
        },
        error: () => {
          this.uploadMessage.set('Upload failed. Check the API.');
          this.uploading.set(false);
        }
      });
      

    } catch (error) {
      this.uploadMessage.set(
        error instanceof Error ? error.message : 'Invalid CSV file.'
      );
      this.uploading.set(false);
    }

    input.value = '';
  }


  chartPoints(points: SpectrumPoint[]): string {
    if (points.length === 0) return '';

    const sorted = [...points].sort(
      (a, b) => a.wavelength - b.wavelength
    );

    const wavelengths = sorted.map(p => p.wavelength);
    const intensities = sorted.map(p => p.intensity);

    const minX = Math.min(...wavelengths);
    const maxX = Math.max(...wavelengths);
    const minY = Math.min(...intensities);
    const maxY = Math.max(...intensities);

    const left = 50;
    const right = 670;
    const top = 20;
    const bottom = 220;

    return sorted.map(point => {
      const x = left +
        ((point.wavelength - minX) / (maxX - minX || 1)) *
        (right - left);

      const y = bottom -
        ((point.intensity - minY) / (maxY - minY || 1)) *
        (bottom - top);

      return `${x.toFixed(2)},${y.toFixed(2)}`;
    }).join(' ');
  }


  private readonly http = inject(HttpClient);

  readonly measurements = signal<Measurement[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

  constructor() {
    this.http.get<Measurement[]>(
      'http://localhost:5071/api/measurements'
    ).subscribe({

      next: data => {
        this.measurements.set(data);
        this.loading.set(false);

        // Request analysis for each saved measurement.
        data.forEach(measurement => {
          this.loadAnalysis(measurement.id);
        });
      },

      error: () => {
        this.error.set('Unable to connect to the SpectraLab API.');
        this.loading.set(false);
      }
    });
  }
}
