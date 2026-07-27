import { useEffect, useState, type SubmitEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError, apiFetch } from '@/lib/api'
import type { GroupPreviewResponse, JoinGroupResponse } from '@/lib/types'
import { useAuthStore } from '@/stores/authStore'
import { useGuestStore } from '@/stores/guestStore'

export function JoinInvitePage() {
  const { inviteCode } = useParams<{ inviteCode: string }>()
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)
  const setGuestMembership = useGuestStore((state) => state.setMembership)

  const [preview, setPreview] = useState<GroupPreviewResponse | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [guestName, setGuestName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isJoining, setIsJoining] = useState(false)

  useEffect(() => {
    if (!inviteCode) return

    apiFetch<GroupPreviewResponse>(`/api/groups/join/${inviteCode}`, { auth: false })
      .then(setPreview)
      .catch(() => setNotFound(true))
  }, [inviteCode])

  async function joinAs(displayName: string, auth: boolean) {
    if (!preview) return
    setError(null)
    setIsJoining(true)

    try {
      const result = await apiFetch<JoinGroupResponse>(`/api/groups/${preview.groupId}/join`, {
        method: 'POST',
        body: { displayName },
        auth,
      })

      if (result.isGuest) {
        setGuestMembership(result.groupId, { memberId: result.memberId, displayName: result.displayName })
      }

      navigate(`/groups/${result.groupId}`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
      setIsJoining(false)
    }
  }

  function handleGuestSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    void joinAs(guestName, false)
  }

  if (notFound) {
    return (
      <div className="mx-auto max-w-sm text-center">
        <h1 className="text-xl font-semibold">Invite not found</h1>
        <p className="mt-2 text-muted-foreground">This invite link is invalid or has expired.</p>
      </div>
    )
  }

  if (!preview) {
    return <p className="mx-auto max-w-sm text-center text-muted-foreground">Loading...</p>
  }

  return (
    <div className="mx-auto max-w-sm">
      <Card>
        <CardHeader>
          <CardTitle>Join "{preview.groupName}"</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">
            Members: {preview.memberNames.join(', ')}
          </p>

          {error && <p className="text-sm text-destructive">{error}</p>}

          {user ? (
            <Button onClick={() => void joinAs(user.displayName, true)} disabled={isJoining}>
              {isJoining ? 'Joining...' : `Join as ${user.displayName}`}
            </Button>
          ) : (
            <form onSubmit={handleGuestSubmit} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="guestName">Your name</Label>
                <Input
                  id="guestName"
                  value={guestName}
                  onChange={(e) => setGuestName(e.target.value)}
                  required
                  autoFocus
                />
              </div>
              <Button type="submit" disabled={isJoining}>
                {isJoining ? 'Joining...' : 'Join as guest'}
              </Button>
              <p className="text-center text-sm text-muted-foreground">
                Have an account?{' '}
                <Link to="/login" className="text-primary hover:underline">
                  Log in
                </Link>{' '}
                first to join with it.
              </p>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
