import { useMemo } from 'react';
import clsx from 'clsx';
import type { SeatMapSeatDto, ZoneSide } from '@kfs/types';
import { SeatStatus } from '@kfs/types';
import { chunk } from '@kfs/utils';

interface SeatGridProps {
  seats: SeatMapSeatDto[];
  side: ZoneSide;
  selectedSeatId?: string | null;
  onSelect?: (seat: SeatMapSeatDto) => void;
  // Optional: highlight a specific row+seat in a different colour (used to show the auto-paired
  // mirror seat on the opposite side).
  mirrorRow?: string | null;
  mirrorSeatNumber?: number | null;
}

export function SeatGrid({ seats, side, selectedSeatId, onSelect, mirrorRow, mirrorSeatNumber }: SeatGridProps) {
  const rows = useMemo(() => {
    const grouped = new Map<string, SeatMapSeatDto[]>();
    for (const s of seats) {
      const list = grouped.get(s.rowLabel) ?? [];
      list.push(s);
      grouped.set(s.rowLabel, list);
    }
    return [...grouped.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([row, list]) => [row, list.sort((a, b) => a.seatNumber - b.seatNumber)] as const);
  }, [seats]);

  // Side header — Female (pink) and Male (blue) per the venue colour code in the brand book.
  const sideTone = side === 1
    ? 'border-pink-200 bg-pink-50/60 text-pink-700'
    : 'border-blue-200 bg-blue-50/60 text-blue-700';

  return (
    <div className={clsx('rounded-md border p-3', sideTone)}>
      <div className="mb-3 text-center text-xs font-semibold uppercase tracking-wider">
        {side === 1 ? 'Female (Mother)' : 'Male (Father)'}
      </div>

      <div className="flex flex-col gap-2">
        {rows.map(([rowLabel, rowSeats]) => (
          <div key={rowLabel} className="flex items-center gap-2">
            <span className="w-6 text-center text-xs font-bold text-kfs-forest">{rowLabel}</span>
            {/* visual blocks of 5 seats with a gap between blocks for readability */}
            <div className="flex flex-wrap gap-1">
              {chunk(rowSeats, 5).map((block, bi) => (
                <div key={bi} className="flex gap-1">
                  {block.map((seat) => {
                    const isMirror = mirrorRow === seat.rowLabel && mirrorSeatNumber === seat.seatNumber;
                    const isSelected = selectedSeatId === seat.id || isMirror;
                    const cls = seat.status === SeatStatus.Booked
                      ? 'seat-booked'
                      : seat.status === SeatStatus.Held
                        ? 'seat-held'
                        : isSelected
                          ? 'seat-selected'
                          : 'seat-available';
                    return (
                      <button
                        key={seat.id}
                        type="button"
                        className={cls}
                        disabled={seat.status !== SeatStatus.Available || !onSelect}
                        onClick={() => onSelect?.(seat)}
                        aria-label={`Seat ${seat.fullLabel}`}
                        title={seat.fullLabel}
                      >
                        {seat.seatNumber}
                      </button>
                    );
                  })}
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
