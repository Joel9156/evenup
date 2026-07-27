import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuthStore } from '@/stores/authStore'
import { ApiError, apiFetch } from '@/lib/api'
import type { GroupResponse } from '@/lib/types'

export function GroupDetailPage() {
  const { id } = useParams<{ id: string }>()
  const user = useAuthStore((state) => state.user)
  const [group, setGroup] = useState<GroupResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!id) return
    let cancelled = false

    apiFetch<GroupResponse>(`/api/groups/${id}`, { auth: false })
      .then((data) => {
        if (!cancelled) setGroup(data)
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Failed to load this group.')
      })

    return () => {
      cancelled = true
    }
  }, [id])

  if (error) {
    return <p className="text-sm text-destructive">{error}</p>
  }

  if (!group) {
    return <p className="text-muted-foreground">Loading...</p>
  }

  const inviteLink = `${window.location.origin}/join/${group.inviteCode}`

  function handleCopyInvite() {
    navigator.clipboard.writeText(inviteLink)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">{group.name}</h1>
        <div className="flex gap-2">
          <Button asChild size="sm" variant="outline">
            <Link to={`/groups/${group.id}/expenses/new`}>Add expense</Link>
          </Button>
          <Button asChild size="sm" variant="outline">
            <Link to={`/groups/${group.id}/settle`}>Settle up</Link>
          </Button>
          {user && (
            <Button asChild size="sm" variant="outline">
              <Link to={`/groups/${group.id}/chat`}>AI chat</Link>
            </Button>
          )}
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Members ({group.members.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <ul className="flex flex-col gap-1.5 text-sm">
            {group.members.map((member) => (
              <li key={member.id} className="flex items-center gap-2">
                {member.displayName}
                {member.isGuest && <span className="text-xs text-muted-foreground">(guest)</span>}
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Invite others</CardTitle>
        </CardHeader>
        <CardContent className="flex items-center gap-2">
          <code className="flex-1 truncate rounded bg-muted px-2 py-1 text-xs">{inviteLink}</code>
          <Button size="sm" variant="outline" onClick={handleCopyInvite}>
            {copied ? 'Copied!' : 'Copy link'}
          </Button>
        </CardContent>
      </Card>

      <p className="text-sm text-muted-foreground">Expense list and balances coming soon.</p>
    </div>
  )
}
