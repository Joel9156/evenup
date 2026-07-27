import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useMyMemberId } from '@/hooks/useMyMemberId'
import { ApiError, apiFetch } from '@/lib/api'
import type { BalancesResponse, ExpenseResponse, GroupResponse } from '@/lib/types'
import { useAuthStore } from '@/stores/authStore'

export function GroupDetailPage() {
  const { id } = useParams<{ id: string }>()
  const user = useAuthStore((state) => state.user)
  const [group, setGroup] = useState<GroupResponse | null>(null)
  const [expenses, setExpenses] = useState<ExpenseResponse[] | null>(null)
  const [balances, setBalances] = useState<BalancesResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const myMemberId = useMyMemberId(group, id)

  const loadExpensesAndBalances = useCallback(() => {
    if (!id) return
    apiFetch<ExpenseResponse[]>(`/api/groups/${id}/expenses`).then(setExpenses).catch(() => setExpenses([]))
    apiFetch<BalancesResponse>(`/api/groups/${id}/balances`).then(setBalances).catch(() => setBalances(null))
  }, [id])

  useEffect(() => {
    if (!id) return
    let cancelled = false

    apiFetch<GroupResponse>(`/api/groups/${id}`)
      .then((data) => {
        if (!cancelled) setGroup(data)
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Failed to load this group.')
      })

    loadExpensesAndBalances()

    return () => {
      cancelled = true
    }
  }, [id, loadExpensesAndBalances])

  async function handleDeleteExpense(expenseId: string) {
    if (!confirm('Delete this expense?')) return

    try {
      await apiFetch(`/api/expenses/${expenseId}`, { method: 'DELETE' })
      loadExpensesAndBalances()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to delete this expense.')
    }
  }

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
          <CardTitle>Balances</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {balances === null ? (
            <p className="text-sm text-muted-foreground">Loading...</p>
          ) : (
            <>
              <ul className="flex flex-col gap-1 text-sm">
                {balances.netBalances.map((b) => (
                  <li key={b.memberId} className="flex justify-between">
                    <span>{b.displayName}</span>
                    <span className={b.netBalance >= 0 ? 'text-emerald-600' : 'text-destructive'}>
                      {b.netBalance >= 0 ? `is owed $${b.netBalance.toFixed(2)}` : `owes $${Math.abs(b.netBalance).toFixed(2)}`}
                    </span>
                  </li>
                ))}
              </ul>
              {balances.suggestedTransactions.length > 0 && (
                <div className="border-t pt-3 text-sm text-muted-foreground">
                  {balances.suggestedTransactions.map((t, i) => (
                    <p key={i}>
                      {t.fromDisplayName} owes {t.toDisplayName} ${t.amount.toFixed(2)}
                    </p>
                  ))}
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Expenses</CardTitle>
        </CardHeader>
        <CardContent>
          {expenses === null ? (
            <p className="text-sm text-muted-foreground">Loading...</p>
          ) : expenses.length === 0 ? (
            <p className="text-sm text-muted-foreground">No expenses yet.</p>
          ) : (
            <ul className="flex flex-col gap-3 text-sm">
              {expenses.map((expense) => (
                <li key={expense.id} className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium">{expense.description}</p>
                    <p className="text-muted-foreground">
                      ${expense.totalAmount.toFixed(2)} paid by {expense.paidByDisplayName}
                    </p>
                  </div>
                  {user && myMemberId === expense.createdByMemberId && (
                    <Button size="sm" variant="ghost" onClick={() => void handleDeleteExpense(expense.id)}>
                      Delete
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
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
    </div>
  )
}
