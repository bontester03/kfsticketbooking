import type { BookingItemDto } from '@kfs/types';

interface TicketCardProps {
  item: BookingItemDto;
  studentName: string;
  studentEmail: string;
  parentLabel: string;
  group: 'A' | 'B';
}

/**
 * Wide ticket layout matching the printed reference: a stub on the left with seat
 * details, a receipt on the right with recipient + QR, separated by a dashed
 * perforation line. Circular notches on the outer edges complete the ticket-stub feel.
 */
export function TicketCard({ item, studentName, studentEmail, parentLabel, group }: TicketCardProps) {
  const last6 = (item.ticketNumber || '').slice(-6).padStart(6, '*');
  const seatLabel = `${item.rowLabel}${item.seatNumber}`;

  return (
    <div className="ticket">
      <span className="ticket-notch ticket-notch-left" aria-hidden="true" />
      <span className="ticket-notch ticket-notch-right" aria-hidden="true" />

      {/* Left half — stub with seat details */}
      <section className="ticket-stub">
        <div className="ticket-num">#****{last6}</div>
        <hr className="ticket-rule" />

        <div>
          <div className="ticket-label">CATEGORY</div>
          <div className="ticket-category" aria-label={`Category ${group}`}>{group}</div>
        </div>

        <div className="ticket-info-grid">
          <div>
            <div className="ticket-label">GATE</div>
            <div className="ticket-value">Gate {group}</div>
          </div>
          <div>
            <div className="ticket-label">BLOCK</div>
            <div className="ticket-value">{item.block}</div>
          </div>
          <div>
            <div className="ticket-label">SEAT</div>
            <div className="ticket-value">{item.seatNumber}</div>
          </div>
          <div>
            <div className="ticket-label">ROW</div>
            <div className="ticket-value">{item.rowLabel}</div>
          </div>
        </div>

        <hr className="ticket-rule" />

        <div dir="rtl" lang="ar" className="ticket-arabic">
          المقاعد المحجوزة: {seatLabel}
        </div>

        <div className="ticket-parent-line">
          {parentLabel} of {studentName}
        </div>
      </section>

      {/* Right half — receipt with QR */}
      <section className="ticket-receipt">
        <ClockIcon />
        <p className="ticket-receipt-line">Ticket is sent to</p>
        <p className="ticket-email">{studentEmail}</p>
        <p className="ticket-receipt-line">and pending approval<br />by receiver</p>

        <p className="ticket-qr-label">QR Code:</p>
        {item.qrCodeImageUrl ? (
          <img alt={`QR ${item.ticketNumber}`} src={item.qrCodeImageUrl} className="ticket-qr" />
        ) : (
          <div className="ticket-qr ticket-qr-missing">QR pending</div>
        )}
      </section>
    </div>
  );
}

/** Stopwatch-style icon — rendered in KFS gold via `currentColor`. */
function ClockIcon() {
  return (
    <svg viewBox="0 0 24 24" className="ticket-clock" aria-hidden="true" fill="none">
      <circle cx="12" cy="13" r="8" stroke="currentColor" strokeWidth="1.6" />
      <line x1="10" y1="2" x2="14" y2="2" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <line x1="12" y1="2" x2="12" y2="5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <line x1="12" y1="6.5" x2="12" y2="7.8" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <line x1="17.5" y1="13" x2="16.2" y2="13" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <line x1="12" y1="19.5" x2="12" y2="18.2" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <line x1="6.5" y1="13" x2="7.8" y2="13" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <path d="M12 8.5 V13 H15.5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
