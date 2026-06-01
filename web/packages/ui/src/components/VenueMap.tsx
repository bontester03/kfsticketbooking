import { useMemo } from 'react';
import clsx from 'clsx';
import type { SeatMapDto, SeatMapSeatDto } from '@kfs/types';
import { SeatStatus, ZoneSide } from '@kfs/types';

interface SeatRef {
  group: 'A' | 'B';
  side: ZoneSide;
  rowLabel: string;
  seatNumber: number;
}

interface VenueMapProps {
  groupA: SeatMapDto;
  groupB: SeatMapDto;
  /** Seat the user has tentatively picked (client-side only, no API hit yet). */
  pendingSelection?: SeatRef | null;
  /** The user's already-confirmed seat (renders distinctively, non-clickable). */
  confirmedSeat?: SeatRef | null;
  /** When true, every seat is non-interactive — used after a booking is confirmed. */
  readOnly?: boolean;
  /** Disable clicks while a mutation is in flight. */
  disabled?: boolean;
  /** Called on click when the seat is available and the map isn't read-only. */
  onSelect: (args: SeatRef & { seatId: string }) => void;
}

/**
 * Floor-plan view of the KFS auditorium. Mirrors `booking stage KFS.pdf`.
 *
 * In booking-mode (`readOnly=false`) clicks fire `onSelect` with the picked seat;
 * the parent decides whether to commit it. In read-only mode (after the student has
 * confirmed) all seats are non-interactive and the student's own pair is highlighted
 * with `venue-seat-yours`.
 */
export function VenueMap({
  groupA, groupB, pendingSelection, confirmedSeat, readOnly, disabled, onSelect
}: VenueMapProps) {
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
            zones={groupB.zones}
            pendingSelection={pendingSelection?.group === 'B' ? pendingSelection : null}
            confirmedSeat={confirmedSeat?.group === 'B' ? confirmedSeat : null}
            readOnly={readOnly}
            disabled={disabled}
            onSelect={onSelect}
          />
          <VipBlock
            label="VIP A"
            group="A"
            zones={groupA.zones}
            pendingSelection={pendingSelection?.group === 'A' ? pendingSelection : null}
            confirmedSeat={confirmedSeat?.group === 'A' ? confirmedSeat : null}
            readOnly={readOnly}
            disabled={disabled}
            onSelect={onSelect}
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

      <Legend showYours={!!confirmedSeat} />
    </div>
  );
}

interface VipBlockProps {
  label: string;
  group: 'A' | 'B';
  /** 2 zones for the boys event (Female-side + Male-side), 1 for the girls event (single block). */
  zones: SeatMapDto['zones'];
  pendingSelection: { side: ZoneSide; rowLabel: string; seatNumber: number } | null;
  confirmedSeat: { side: ZoneSide; rowLabel: string; seatNumber: number } | null;
  readOnly?: boolean;
  disabled?: boolean;
  onSelect: VenueMapProps['onSelect'];
}

function VipBlock({
  label, group, zones, pendingSelection, confirmedSeat, readOnly, disabled, onSelect
}: VipBlockProps) {
  const sumSeats = zones.reduce((n, z) => n + z.seats.length, 0);
  const isSingleBlock = zones.length === 1;
  const meta = isSingleBlock
    ? `· ${sumSeats} seats · Mother & Grandmother pair`
    : `· ${zones.length} sides × ${zones[0]?.seats.length ?? 0} seats`;
  return (
    <div className="venue-vip">
      <header className="venue-vip-header">
        {label} <span className="venue-vip-meta">{meta}</span>
      </header>
      <div className={isSingleBlock ? 'venue-vip-sides-single' : 'venue-vip-sides'}>
        {zones.map((z) => {
          // Boys: zone.Side is Female=1 or Male=2; girls: Side=None=0 (single block).
          const tone: 'female' | 'male' | 'single' =
            z.side === ZoneSide.Female ? 'female' :
            z.side === ZoneSide.Male   ? 'male'   :
                                         'single';
          return (
            <SidePane
              key={z.zoneId}
              tone={tone}
              seats={z.seats}
              group={group}
              side={z.side}
              pendingSelection={pendingSelection}
              confirmedSeat={confirmedSeat}
              readOnly={readOnly}
              disabled={disabled}
              onSelect={onSelect}
            />
          );
        })}
      </div>
    </div>
  );
}

interface SidePaneProps {
  tone: 'female' | 'male' | 'single';
  seats: SeatMapSeatDto[];
  group: 'A' | 'B';
  side: ZoneSide;
  pendingSelection: { side: ZoneSide; rowLabel: string; seatNumber: number } | null;
  confirmedSeat: { side: ZoneSide; rowLabel: string; seatNumber: number } | null;
  readOnly?: boolean;
  disabled?: boolean;
  onSelect: VenueMapProps['onSelect'];
}

function SidePane({
  tone, seats, group, side, pendingSelection, confirmedSeat, readOnly, disabled, onSelect
}: SidePaneProps) {
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
      <div className="venue-side-label">{
        tone === 'female' ? 'Female (Mother)' :
        tone === 'male'   ? 'Male (Father)'   :
                            'Mother & Grandmother (adjacent pair)'
      }</div>
      <div className="venue-rows">
        {rows.map(([rowLabel, rowSeats]) => (
          <div key={rowLabel} className="venue-row">
            <span className="venue-row-label">{rowLabel}</span>
            <div className="venue-row-seats">
              {rowSeats.map((seat) => {
                const matchesYours =
                  !!confirmedSeat &&
                  confirmedSeat.rowLabel === seat.rowLabel &&
                  confirmedSeat.seatNumber === seat.seatNumber;

                const matchesPending =
                  !!pendingSelection &&
                  pendingSelection.rowLabel === seat.rowLabel &&
                  pendingSelection.seatNumber === seat.seatNumber;

                const isPickedThisSide = matchesPending && pendingSelection!.side === side;
                const isMirrorThisSide = matchesPending && pendingSelection!.side !== side;

                // The user's own holds (cart) and confirmed seats win over the generic
                // booked/held styling — that way they can always see their own pair clearly
                // even though the seat is also Held in the API response from their own hold.
                const cls = matchesYours
                  ? 'venue-seat venue-seat-yours'
                  : isPickedThisSide
                    ? 'venue-seat venue-seat-picked'
                    : isMirrorThisSide
                      ? 'venue-seat venue-seat-mirror'
                      : seat.status === SeatStatus.Booked
                        ? 'venue-seat venue-seat-booked'
                        : seat.status === SeatStatus.Held
                          ? 'venue-seat venue-seat-held'
                          : 'venue-seat venue-seat-available';

                const interactive = !readOnly && !disabled && seat.status === SeatStatus.Available && !matchesYours;

                return (
                  <button
                    key={seat.id}
                    type="button"
                    className={cls}
                    disabled={!interactive}
                    onClick={() => interactive && onSelect({ group, side, rowLabel: seat.rowLabel, seatNumber: seat.seatNumber, seatId: seat.id })}
                    aria-label={`${tone === 'female' ? 'Female' : 'Male'} side, row ${seat.rowLabel}, seat ${seat.seatNumber}`}
                    title={matchesYours ? `Your seat — ${seat.fullLabel}` : seat.fullLabel}
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

function Legend({ showYours }: { showYours: boolean }) {
  return (
    <div className="venue-legend">
      <span><i className="venue-swatch venue-seat-available" /> Available</span>
      <span><i className="venue-swatch venue-seat-held" /> Held</span>
      <span><i className="venue-swatch venue-seat-booked" /> Booked</span>
      <span><i className="venue-swatch venue-seat-mirror" /> Auto-paired mirror</span>
      {showYours && <span><i className="venue-swatch venue-seat-yours" /> Your seat</span>}
    </div>
  );
}
