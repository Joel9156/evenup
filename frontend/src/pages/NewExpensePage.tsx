import { useEffect, useState, type SubmitEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useMyMemberId } from '@/hooks/useMyMemberId'
import { ApiError, apiFetch } from '@/lib/api'
import type { GroupResponse } from '@/lib/types'

interface ShareState {
  included: boolean
  amount: string
}

export function NewExpensePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [group, setGroup] = useState<GroupResponse | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [description, setDescription] = useState('')
  const [totalAmount, setTotalAmount] = useState('')
  const [paidByMemberId, setPaidByMemberId] = useState('')
  const [shares, setShares] = useState<Record<string, ShareState>>({})
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    if (!id) return

    apiFetch<GroupResponse>(`/api/groups/${id}`)
      .then((data) => {
        setGroup(data)
        setShares(Object.fromEntries(data.members.map((m) => [m.id, { included: true, amount: '' }])))
      })
      .catch(() => setNotFound(true))
  }, [id])

  const myMemberId = useMyMemberId(group, id)

  useEffect(() => {
    if (myMemberId) setPaidByMemberId(myMemberId)
  }, [myMemberId])

  function handleSplitEvenly() {
    const includedIds = Object.entries(shares)
      .filter(([, s]) => s.included)
      .map(([memberId]) => memberId)
    const total = Number(totalAmount)
    if (!total || includedIds.length === 0) return

    const evenShare = (total / includedIds.length).toFixed(2)
    setShares((prev) => {
      const next = { ...prev }
      for (const memberId of includedIds) {
        next[memberId] = { ...next[memberId], amount: evenShare }
      }
      return next
    })
  }

  function toggleIncluded(memberId: string) {
    setShares((prev) => ({
      ...prev,
      [memberId]: { ...prev[memberId], included: !prev[memberId].included },
    }))
  }

  function setShareAmount(memberId: string, amount: string) {
    setShares((prev) => ({ ...prev, [memberId]: { ...prev[memberId], amount } }))
  }

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!group || !myMemberId) return
    setError(null)
    setIsSubmitting(true)

    try {
      await apiFetch(`/api/groups/${group.id}/expenses`, {
        method: 'POST',
        body: {
          description,
          totalAmount: Number(totalAmount),
          paidByMemberId,
          createdByMemberId: myMemberId,
          shares: Object.entries(shares)
            .filter(([, s]) => s.included && Number(s.amount) > 0)
            .map(([memberId, s]) => ({ memberId, amount: Number(s.amount) })),
        },
      })
      navigate(`/groups/${group.id}`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setIsSubmitting(false)
    }
  }

  if (notFound) {
    return <p className="text-sm text-destructive">Group not found.</p>
  }

  if (!group) {
    return <p className="text-muted-foreground">Loading...</p>
  }

  if (!myMemberId) {
    return (
      <div className="mx-auto max-w-sm text-center">
        <h1 className="text-xl font-semibold">Join this group first</h1>
        <p className="mt-2 text-muted-foreground">You need to be a member to add an expense.</p>
        <Button asChild className="mt-4">
          <Link to={`/join/${group.inviteCode}`}>Join {group.name}</Link>
        </Button>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-md">
      <Card>
        <CardHeader>
          <CardTitle>Add an expense</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="description">Description</Label>
              <Input
                id="description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                required
                autoFocus
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="totalAmount">Total amount</Label>
              <Input
                id="totalAmount"
                type="number"
                min="0.01"
                step="0.01"
                value={totalAmount}
                onChange={(e) => setTotalAmount(e.target.value)}
                required
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="paidBy">Paid by</Label>
              <select
                id="paidBy"
                className="h-8 rounded-lg border border-input bg-background px-2.5 text-sm"
                value={paidByMemberId}
                onChange={(e) => setPaidByMemberId(e.target.value)}
              >
                {group.members.map((member) => (
                  <option key={member.id} value={member.id}>
                    {member.displayName}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-2">
              <div className="flex items-center justify-between">
                <Label>Split between</Label>
                <Button type="button" variant="outline" size="sm" onClick={handleSplitEvenly}>
                  Split evenly
                </Button>
              </div>
              {group.members.map((member) => (
                <div key={member.id} className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={shares[member.id]?.included ?? false}
                    onChange={() => toggleIncluded(member.id)}
                    className="size-4"
                  />
                  <span className="flex-1 text-sm">{member.displayName}</span>
                  <Input
                    type="number"
                    min="0"
                    step="0.01"
                    className="w-24"
                    disabled={!shares[member.id]?.included}
                    value={shares[member.id]?.amount ?? ''}
                    onChange={(e) => setShareAmount(member.id, e.target.value)}
                  />
                </div>
              ))}
            </div>

            {error && <p className="text-sm text-destructive">{error}</p>}
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Adding...' : 'Add expense'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
