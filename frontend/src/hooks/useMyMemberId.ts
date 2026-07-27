import { useMemo } from 'react'
import type { GroupResponse } from '@/lib/types'
import { useAuthStore } from '@/stores/authStore'
import { useGuestStore } from '@/stores/guestStore'

// Resolves "which member row in this group is me" — by matching the signed-in user's id,
// or (for guests, who have no JWT) by looking up the member id remembered at join time.
export function useMyMemberId(group: GroupResponse | null, groupId: string | undefined) {
  const user = useAuthStore((state) => state.user)
  const guestMemberships = useGuestStore((state) => state.memberships)

  return useMemo(() => {
    if (!group || !groupId) return undefined
    if (user) return group.members.find((m) => m.userId === user.id)?.id
    return guestMemberships[groupId]?.memberId
  }, [group, user, guestMemberships, groupId])
}
