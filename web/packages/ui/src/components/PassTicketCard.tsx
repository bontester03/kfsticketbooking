import type { AdminPassDto, AdminPassType } from '@kfs/types';

interface PassTicketCardProps {
  pass: AdminPassDto;
}

interface TypeMeta {
  label: string;
  letter: string;
  gate: string;
  badgeClass: string;
  arabic: string;
}

// Intelligent per-type theming — accent, gate and zone change with the pass type.
const TYPE_META: Record<AdminPassType, TypeMeta> = {
  0: { label: 'VVIP',       letter: 'V', gate: 'Gate V', badgeClass: 'ticket-category--vvip',  arabic: 'كبار الشخصيات' },
  1: { label: 'Guest',      letter: 'G', gate: 'Gate G', badgeClass: 'ticket-category--guest', arabic: 'ضيف' },
  2: { label: 'Staff',      letter: 'S', gate: 'Gate S', badgeClass: 'ticket-category--staff', arabic: 'طاقم العمل' },
  3: { label: 'Media',      letter: 'M', gate: 'Gate M', badgeClass: 'ticket-category--media', arabic: 'إعلام' },
  4: { label: 'Photographer',     letter: 'P', gate: 'Gate P', badgeClass: 'ticket-category--media', arabic: 'مصور' },
  5: { label: 'Personal Assistant', letter: 'A', gate: 'Gate A', badgeClass: 'ticket-category--staff', arabic: 'مساعد شخصي' },
  6: { label: 'Visitor',    letter: 'V', gate: 'Gate V', badgeClass: 'ticket-category--guest', arabic: 'زائر' },
  7: { label: 'Emergency',  letter: 'E', gate: 'Gate E', badgeClass: 'ticket-category--vvip',  arabic: 'طوارئ' }
};

/**
 * Printable admin pass in the same visual language as the student TicketCard — stub on the
 * left, QR receipt on the right, dashed perforation between — but adapted for zone passes:
 * no seat pairing, fields become Gate / Zone / Seats / Pass #, and the receipt instructs the
 * holder to present the pass at the gate (these are printed, not emailed).
 */
export function PassTicketCard({ pass }: PassTicketCardProps) {
  const meta = TYPE_META[pass.type] ?? TYPE_META[1];
  const last6 = (pass.ticketNumber || '').slice(-6).padStart(6, '*');
  const isGuest = pass.type === 1;

  // VVIP / Staff / Media: just the QR — no stub, no frame.
  // The ticket number sits small beneath as a manual-lookup fallback if the scan fails.
  if (!isGuest) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl bg-white p-6 ring-1 ring-slate-200">
        {pass.qrCodeImageUrl ? (
          <img alt={`QR ${pass.ticketNumber}`} src={pass.qrCodeImageUrl}
               className="h-60 w-60 rounded ring-1 ring-slate-200" />
        ) : (
          <div className="grid h-60 w-60 place-items-center rounded bg-slate-50 text-xs text-slate-500">QR pending</div>
        )}
        <p className="font-mono text-xs text-slate-400">{pass.ticketNumber}</p>
        {pass.admittedCount > 0 && (
          <p className="rounded-full bg-green-100 px-3 py-1 text-xs font-semibold text-green-800">
            ✓ Scanned at the gate
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="ticket">
      <span className="ticket-notch ticket-notch-left" aria-hidden="true" />
      <span className="ticket-notch ticket-notch-right" aria-hidden="true" />

      {/* Left half — stub with pass details */}
      <section className="ticket-stub">
        <div className="ticket-num">#****{last6}</div>
        <hr className="ticket-rule" />

        <div>
          <div className="ticket-label">CATEGORY</div>
          <div className={`ticket-category ${meta.badgeClass}`} aria-label={`Category ${meta.label}`}>
            {meta.letter}
          </div>
        </div>

        <div className="ticket-info-grid">
          <div>
            <div className="ticket-label">GATE</div>
            <div className="ticket-value">{pass.gate ?? meta.gate}</div>
          </div>
          <div>
            <div className="ticket-label">ZONE</div>
            <div className="ticket-value">{meta.label}</div>
          </div>
          <div>
            <div className="ticket-label">SEATS</div>
            <div className="ticket-value">{pass.seatsCount}</div>
          </div>
          <div>
            <div className="ticket-label">PASS #</div>
            <div className="ticket-value">{pass.sequenceNumber.toString().padStart(3, '0')}</div>
          </div>
        </div>

        <hr className="ticket-rule" />

        <div dir="rtl" lang="ar" className="ticket-arabic">
          المنطقة: {meta.arabic}
        </div>

        <div className="ticket-parent-line">
          {pass.issuedToName ? `Issued to ${pass.issuedToName}` : 'General admission'}
        </div>
      </section>

      {/* Right half — receipt with QR. Single-admit passes (VVIP/Staff/Media) show just the QR;
          Guest keeps the headline because its 3-admit count makes the context meaningful. */}
      <section className="ticket-receipt">
        {pass.type === 1 /* Guest */ && (
          <>
            <p className="ticket-receipt-line">Present this pass</p>
            <p className="ticket-email">at the gate</p>
            <p className="ticket-receipt-line">{meta.label} zone</p>
            <p className="ticket-qr-label">QR Code:</p>
          </>
        )}
        {pass.qrCodeImageUrl ? (
          <img alt={`QR ${pass.ticketNumber}`} src={pass.qrCodeImageUrl} className="ticket-qr" />
        ) : (
          <div className="ticket-qr ticket-qr-missing">QR pending</div>
        )}
        {pass.admittedCount > 0 && (
          <p className="mt-2 inline-flex items-center gap-1 rounded-full bg-green-100 px-3 py-1 text-xs font-semibold text-green-800">
            ✓ {pass.admittedCount} of {pass.seatsCount} admitted
          </p>
        )}
      </section>
    </div>
  );
}
