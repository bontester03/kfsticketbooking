import { Pipe, PipeTransform } from '@angular/core';
import { BookingStatus } from '@core/models/booking.model';

@Pipe({ name: 'statusLabel', standalone: true })
export class StatusLabelPipe implements PipeTransform {
  transform(status: BookingStatus): string {
    return status;
  }
}
