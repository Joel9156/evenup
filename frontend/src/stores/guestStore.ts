import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface GuestMembership {
  memberId: string
  displayName: string
}

interface GuestState {
  // Guests have no JWT to prove who they are, so once a guest joins a group we remember
  // "which member row is me" locally, keyed by group id — this is what lets a guest come
  // back and add expenses as themselves without ever creating an account.
  memberships: Record<string, GuestMembership>
  setMembership: (groupId: string, membership: GuestMembership) => void
}

export const useGuestStore = create<GuestState>()(
  persist(
    (set) => ({
      memberships: {},
      setMembership: (groupId, membership) =>
        set((state) => ({ memberships: { ...state.memberships, [groupId]: membership } })),
    }),
    { name: 'evenup-guest' },
  ),
)
