import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { EventGender } from '@kfs/types';

// Holds the currently-selected event (boys / girls) for an admin session.
// Set by EventPickerPage on click, and re-synced by Layout from the URL `:eventSlug`
// param so a fresh-tab visit to /boys/dashboard still works without re-picking.
//
// The api.ts axios interceptor reads `eventId` from this store and appends it as
// ?eventId= to every /admin/* request — so individual pages don't need to thread
// it through every fetch call themselves.

export interface EventContextState {
  eventId: string | null;
  eventSlug: string | null;
  eventName: string | null;
  pairLabel: string | null;
  guestSeatsPerPass: number | null;
  gender: EventGender | null;
  setEvent: (e: {
    id: string;
    slug: string;
    name: string;
    pairLabel: string;
    guestSeatsPerPass: number;
    gender: EventGender;
  }) => void;
  clear: () => void;
}

export const useEventContext = create<EventContextState>()(
  persist(
    (set) => ({
      eventId: null,
      eventSlug: null,
      eventName: null,
      pairLabel: null,
      guestSeatsPerPass: null,
      gender: null,
      setEvent: (e) => set({
        eventId: e.id,
        eventSlug: e.slug,
        eventName: e.name,
        pairLabel: e.pairLabel,
        guestSeatsPerPass: e.guestSeatsPerPass,
        gender: e.gender
      }),
      clear: () => set({
        eventId: null, eventSlug: null, eventName: null,
        pairLabel: null, guestSeatsPerPass: null, gender: null
      })
    }),
    { name: 'kfs.admin.event' }
  )
);
