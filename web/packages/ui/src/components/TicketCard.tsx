import type { BookingItemDto } from '@kfs/types';
import { ZoneBadge } from './ZoneBadge';
import { KfsLogo } from './KfsLogo';

interface TicketCardProps {
  item: BookingItemDto;
  studentName: string;
  parentLabel: string;
  group: 'A' | 'B';
}

export function TicketCard({ item, studentName, parentLabel, group }: TicketCardProps) {
  const last6 = item.ticketNumber.slice(-6) || '------';
  const arabicSeat = `${item.rowLabel}${item.seatNumber}`;

  return (
    <div className="surface overflow-hidden">
      <div className="flex items-center justify-between bg-kfs-forest px-5 py-3 text-kfs-forest-50">
        <KfsLogo variant="emblem" />
        <ZoneBadge label={item.block} className="bg-kfs-gold text-white" />
      </div>
      <div className="grid grid-cols-2 gap-x-6 gap-y-2 px-5 py-4 text-sm text-kfs-forest-700">
        <span className="text-kfs-sage-700">#</span>
        <span className="font-mono">****{last6}</span>

        <span className="text-kfs-sage-700">CATEGORY</span>
        <span className="font-semibold">{group}</span>

        <span className="text-kfs-sage-700">GATE</span>
        <span>Gate {group}</span>

        <span className="text-kfs-sage-700">BLOCK</span>
        <span>{item.block}</span>

        <span className="text-kfs-sage-700">ROW</span>
        <span>{item.rowLabel}</span>

        <span className="text-kfs-sage-700">SEAT</span>
        <span className="font-semibold tabular-nums">{item.seatNumber}</span>

        <span className="text-kfs-sage-700">PARENT</span>
        <span>{parentLabel} of {studentName}</span>
      </div>
      <div className="border-t border-kfs-sage-100 px-5 py-3 text-right text-sm font-arabic text-kfs-forest" dir="rtl">
        المقاعد المحجوزة: {arabicSeat}
      </div>
      {item.qrCodeImageUrl ? (
        <div className="border-t border-kfs-sage-100 bg-kfs-forest-50/30 px-5 py-4 text-center">
          <img
            alt={`QR ${item.ticketNumber}`}
            src={item.qrCodeImageUrl}
            className="mx-auto h-40 w-40 rounded-md ring-1 ring-kfs-sage-100"
          />
        </div>
      ) : null}
    </div>
  );
}
