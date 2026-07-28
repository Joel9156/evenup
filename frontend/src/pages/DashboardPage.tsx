import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError, apiFetch } from '@/lib/api'
import type { GroupResponse, MeResponse } from '@/lib/types'

export function DashboardPage() {
  const [groups, setGroups] = useState<GroupResponse[] | null>(null)
  const [me, setMe] = useState<MeResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    apiFetch<GroupResponse[]>('/api/groups')
      .then((data) => {
        if (!cancelled) setGroups(data)
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Failed to load your groups.')
      })

    apiFetch<MeResponse>('/api/auth/me')
      .then((data) => {
        if (!cancelled) setMe(data)
      })
      .catch(() => {
        // Non-critical: the account nudge below just stays hidden if this fails.
      })

    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">My groups</h1>
        <Button asChild size="sm">
          <Link to="/groups/new">New group</Link>
        </Button>
      </div>

      {me && !me.hasAccountNumber && (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-primary/20 bg-primary/5 px-4 py-3">
          <p className="text-sm text-foreground">
            Add your bank account so friends can settle up with you in one click.
          </p>
          <Button asChild size="sm" variant="outline">
            <Link to="/profile">Add account</Link>
          </Button>
        </div>
      )}

      {error && <p className="mt-4 text-sm text-destructive">{error}</p>}

      {groups === null && !error && (
        <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Card key={i}>
              <CardHeader>
                <Skeleton className="h-5 w-2/3" />
              </CardHeader>
              <CardContent>
                <Skeleton className="h-4 w-1/3" />
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {groups?.length === 0 && (
        <p className="mt-4 text-muted-foreground">You're not in any groups yet. Create one to get started.</p>
      )}

      <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {groups?.map((group) => (
          <Link key={group.id} to={`/groups/${group.id}`}>
            <Card className="h-full transition-colors hover:bg-muted/50">
              <CardHeader>
                <CardTitle>{group.name}</CardTitle>
              </CardHeader>
              <CardContent className="text-sm text-muted-foreground">
                {group.members.length} member{group.members.length === 1 ? '' : 's'}
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  )
}
