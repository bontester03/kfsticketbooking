import { useMemo } from 'react';
import clsx from 'clsx';
import type { SeatMapDto, SeatMapSeatDto } from '@kfs/types';
import { SeatStatus, ZoneSide } from '@kfs/types';

interface VenueMapProps {
  groupA: SeatMapDto;
  groupB: SeatMapDto;
  /** When set, the matching {row+seatNumber, opposite side} on the same group is highlighted. */
  pendingSelection?: { group: 'A' | 'B'; side: ZoneSide; rowLabel: string; seatNumber: number } | null;
  onSelect: (args: { group: 'A' | 'B'; side: ZoneSide; rowLabel: string; seatNumber: number; seatId: string }) => void;
  /** Disable interaction (e.g. while a select mutation is in flight). */
  disabled?: boolean;
}

/**
 * Floor-plan view of the KFS auditorium. Mirrors `booking stage KFS.pdf`:
 *
 *                 ┌─────────── Stage ───────────┐
 *
 *   ┌──── VIP B ────┐   ┌──── VIP A ────┐   ┌Staff ┐
 *   │ Female│ Male  │   │ Female│ Male  │   │      │
 *   │       │       │   │       │       │   │ 100  │
 *   └───────┴───────┘   └───────┴───────┘   └──────┘
 *                                            ┌Media ┐
 *   ┌──────── Guest Zone (600) ────────┐    │ 100  │
 *                                            └──────┘
 *
 * Within each Female / Male pane the 4 × 19 seat grid is rendered as clickable
 * buttons.  Guest / Staff / Media zones are admin-issued QR codes — shown
 * read-only here for orientation.
 */
export function VenueMap({ groupA, groupB, pendingSelection, onSelect, disabled }: VenueMapProps) {
  return (
    <div className="venue">
      <div className="venue-stage">
        <span className="venue-stage-label">Stage · المسرح</span>
      </div>

      <div className="venue-floor">
        <div className="venue-vip-row">
          <VipBlock
            label="VIP B"
            group="B"
            femaleZone={groupB.femaleZone}
            maleZone={groupB.maleZone}
            pendingSelection={pendingSelection?.group === 'B' ? pendingSelection : null}
            onSelect={onSelect}
            disabled={disabled}
          />
          <VipBlock
            label="VIP A"
            group="A"
            femaleZone={groupA.femaleZone}
            maleZone={groupA.maleZone}
            pendingSelection={pendingSelection?.group === 'A' ? pendingSelection : null}
            onSelect={onSelect}
            disabled={disabled}
          />
        </div>

        <aside className="venue-right-rail">
          <ZoneTile title="Staff Zone" subtitle="100 seats · admin-issued QR" tone="staff" />
          <ZoneTile title="Media Zone" subtitle="100 seats · admin-issued QR" tone="media" />
        </aside>

        <div className="venue-guest">
          <strong>Guest Zone</strong>
          <span>600 seats · admin issues 200 QR cards × 3 seats each</span>
        </div>
      </div>

      <Legend />
    </div>
  );
}

interface VipBlockProps {
  label: string;
  group: 'A' | 'B';
  femaleZone: SeatMapDto['femaleZone'];
  maleZone: SeatMapDto['maleZone'];
  pendingSelection: { side: ZoneSide; rowLabel: string; seatNumber: number } | null;
  onSelect: VenueMapProps['onSelect'];
  disabled?: boolean;
}

function VipBlock({ label, group, femaleZone, maleZone, pendingSelection, onSelect, disabled }: VipBlockProps) {
  return (
    <div className="venue-vip">
      <header className="venue-vip-header">
        {label} <span className="venue-vip-meta">· 4 rows × 2 sides × 19 seats</span>
      </header>
      <div className="venue-vip-sides">
        <SidePane
          tone="female"
          seats={femaleZone.seats}
          group={group}
          side={ZoneSide.Female}
          pendingSelection={pendingSelection}
          onSelect={onSelect}
          disabled={disabled}
        />
        <SidePane
          tone="male"
          seats={maleZone.seats}
          group={group}
          side={ZoneSide.Male}
          pendingSelection={pendingSelection}
          onSelect={onSelect}
          disabled={disabled}
        />
      </div>
    </div>
  );
}

interface SidePaneProps {
  tone: 'female' | 'male';
  seats: SeatMapSeatDto[];
  group: 'A' | 'B';
  side: ZoneSide;
  pendingSelection: { side: ZoneSide; rowLabel: string; seatNumber: number } | null;
  onSelect: VenueMapProps['onSelect'];
  disabled?: boolean;
}

function SidePane({ tone, seats, group, side, pendingSelection, onSelect, disabled }: SidePaneProps) {
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

  return (
    <div className={clsx('venue-side', `venue-side-${tone}`)}>
      <div className="venue-side-label">{tone === 'female' ? 'Female (Mother)' : 'Male (Father)'}</div>
      <div className="venue-rows">
        {rows.map(([rowLabel, rowSeats]) => (
          <div key={rowLabel} className="venue-row">
            <span className="venue-row-label">{rowLabel}</span>
            <div className="venue-row-seats">
              {rowSeats.map((seat) => {
                const isMirror =
                  pendingSelection &&
                  pendingSelection.rowLabel === seat.rowLabel &&
                  pendingSelection.seatNumber === seat.seatNumber &&
                  pendingSelection.side !== side;
                const isPicked =
                  pendingSelection &&
                  pendingSelection.rowLabel === seat.rowLabel &&
                  pendingSelection.seatNumber === seat.seatNumber &&
                  pendingSelection.side === side;

                const cls = seat.status === SeatStatus.Booked
                  ? 'venue-seat venue-seat-booked'
                  : seat.status === SeatStatus.Held
                    ? 'venue-seat venue-seat-held'
                    : isPicked
                      ? 'venue-seat venue-seat-picked'
                      : isMirror
                        ? 'venue-seat venue-seat-mirror'
                        : 'venue-seat venue-seat-available';

                return (
                  <button
                    key={seat.id}
                    type="button"
                    className={cls}
                    disabled={disabled || seat.status !== SeatStatus.Available}
                    onClick={() => onSelect({ group, side, rowLabel: seat.rowLabel, seatNumber: seat.seatNumber, seatId: seat.id })}
                    aria-label={`${tone === 'female' ? 'Female' : 'Male'} side, row ${seat.rowLabel}, seat ${seat.seatNumber}`}
                    title={`${seat.fullLabel}`}
                  >
                    {seat.seatNumber}
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ZoneTile({ title, subtitle, tone }: { title: string; subtitle: string; tone: 'staff' | 'media' }) {
  return (
    <div className={clsx('venue-zone-tile', `venue-zone-tile-${tone}`)}>
      <div className="venue-zone-tile-title">{title}</div>
      <div className="venue-zone-tile-subtitle">{subtitle}</div>
    </div>
  );
}

function Legend() {
  return (
    <div className="venue-legend">
      <span><i className="venue-swatch venue-seat-available" /> Available</span>
      <span><i className="venue-swatch venue-seat-held" /> Held</span>
      <span><i className="venue-swatch venue-seat-booked" /> Booked</span>
      <span><i className="venue-swatch venue-seat-mirror" /> Mirror seat (auto-paired)</span>
    </div>
  );
}
