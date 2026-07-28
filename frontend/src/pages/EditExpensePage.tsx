import { useEffect, useState, type SubmitEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError, apiFetch } from '@/lib/api'
import type { ExpenseResponse, GroupResponse } from '@/lib/types'

interface ShareState {
  included: boolean
  amount: string
}

export function EditExpensePage() {
  const { id, expenseId } = useParams<{ id: string; expenseId: string }>()
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
    if (!id || !expenseId) return

    Promise.all([
      apiFetch<GroupResponse>(`/api/groups/${id}`),
      apiFetch<ExpenseResponse[]>(`/api/groups/${id}/expenses`),
    ])
      .then(([groupData, expenses]) => {
        const expense = expenses.find((e) => e.id === expenseId)
        if (!expense) {
          setNotFound(true)
          return
        }

        setGroup(groupData)
        setDescription(expense.description)
        setTotalAmount(expense.totalAmount.toFixed(2))
        setPaidByMemberId(expense.paidByMemberId)
        setShares(
          Object.fromEntries(
            groupData.members.map((m) => {
              const share = expense.shares.find((s) => s.memberId === m.id)
              return [m.id, { included: !!share, amount: share ? share.amount.toFixed(2) : '' }]
            }),
          ),
        )
      })
      .catch(() => setNotFound(true))
  }, [id, expenseId])

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
    if (!group || !expenseId) return
    setError(null)
    setIsSubmitting(true)

    try {
      await apiFetch(`/api/expenses/${expenseId}`, {
        method: 'PUT',
        body: {
          description,
          totalAmount: Number(totalAmount),
          paidByMemberId,
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
    return <p className="text-sm text-destructive">Expense not found.</p>
  }

  if (!group) {
    return (
      <div className="mx-auto flex max-w-md flex-col gap-4">
        <Skeleton className="h-4 w-32" />
        <Card>
          <CardHeader>
            <Skeleton className="h-5 w-1/3" />
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-8 w-full" />
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-md">
      <Link to={`/groups/${group.id}`} className="text-sm text-muted-foreground hover:text-foreground">
        ← Back to {group.name}
      </Link>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>Edit expense</CardTitle>
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
              {isSubmitting ? 'Saving...' : 'Save changes'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
