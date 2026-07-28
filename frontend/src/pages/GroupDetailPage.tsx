import { useCallback, useEffect, useState, type SubmitEvent } from 'react'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useMyMemberId } from '@/hooks/useMyMemberId'
import { ApiError, apiFetch } from '@/lib/api'
import type { BalancesResponse, ExpenseResponse, GroupResponse } from '@/lib/types'
import { useAuthStore } from '@/stores/authStore'

// The net balance is a sum across every expense a member touched (as payer and/or
// shareholder) — this reconstructs that per-expense breakdown client-side from the already-
// fetched expense list, so "owes $20.42" can be expanded into the line items behind it.
function memberBreakdown(memberId: string, expenses: ExpenseResponse[]) {
  return expenses
    .map((expense) => {
      const paid = expense.paidByMemberId === memberId ? expense.totalAmount : 0
      const share = expense.shares.find((s) => s.memberId === memberId)?.amount ?? 0
      return { expense, paid, share, net: paid - share }
    })
    .filter((row) => row.paid > 0 || row.share > 0)
}

export function GroupDetailPage() {
  const { id } = useParams<{ id: string }>()
  const user = useAuthStore((state) => state.user)
  const [group, setGroup] = useState<GroupResponse | null>(null)
  const [expenses, setExpenses] = useState<ExpenseResponse[] | null>(null)
  const [balances, setBalances] = useState<BalancesResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [newMemberName, setNewMemberName] = useState('')
  const [isAddingMember, setIsAddingMember] = useState(false)
  const [expandedMemberId, setExpandedMemberId] = useState<string | null>(null)

  const myMemberId = useMyMemberId(group, id)

  const loadGroup = useCallback(() => {
    if (!id) return
    apiFetch<GroupResponse>(`/api/groups/${id}`)
      .then(setGroup)
      .catch((err: unknown) => setError(err instanceof ApiError ? err.message : 'Failed to load this group.'))
  }, [id])

  const loadExpensesAndBalances = useCallback(() => {
    if (!id) return
    apiFetch<ExpenseResponse[]>(`/api/groups/${id}/expenses`).then(setExpenses).catch(() => setExpenses([]))
    apiFetch<BalancesResponse>(`/api/groups/${id}/balances`).then(setBalances).catch(() => setBalances(null))
  }, [id])

  useEffect(() => {
    loadGroup()
    loadExpensesAndBalances()
  }, [loadGroup, loadExpensesAndBalances])

  async function handleDeleteExpense(expenseId: string) {
    if (!confirm('Delete this expense?')) return

    try {
      await apiFetch(`/api/expenses/${expenseId}`, { method: 'DELETE' })
      loadExpensesAndBalances()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to delete this expense.')
    }
  }

  async function handleAddMember(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!id || !newMemberName.trim()) return
    setError(null)
    setIsAddingMember(true)

    try {
      await apiFetch(`/api/groups/${id}/members`, {
        method: 'POST',
        body: { displayName: newMemberName.trim() },
      })
      setNewMemberName('')
      loadGroup()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add this member.')
    } finally {
      setIsAddingMember(false)
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
      <Link to="/dashboard" className="text-sm text-muted-foreground hover:text-foreground">
        ← Back to my groups
      </Link>

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

          {user && (
            <form onSubmit={handleAddMember} className="mt-3 flex gap-2 border-t pt-3">
              <Input
                value={newMemberName}
                onChange={(e) => setNewMemberName(e.target.value)}
                placeholder="Add someone by name, no invite needed"
                className="flex-1"
              />
              <Button type="submit" size="sm" variant="outline" disabled={isAddingMember || !newMemberName.trim()}>
                {isAddingMember ? 'Adding...' : 'Add'}
              </Button>
            </form>
          )}
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
              <ul className="flex flex-col text-sm">
                {balances.netBalances.map((b) => {
                  const isExpanded = expandedMemberId === b.memberId
                  const breakdown = expenses ? memberBreakdown(b.memberId, expenses) : []
                  return (
                    <li key={b.memberId} className="border-b last:border-b-0">
                      <button
                        type="button"
                        className="flex w-full items-center justify-between gap-2 py-2 text-left"
                        onClick={() => setExpandedMemberId(isExpanded ? null : b.memberId)}
                      >
                        <span className="flex items-center gap-1">
                          {isExpanded ? (
                            <ChevronDown className="size-3.5 text-muted-foreground" />
                          ) : (
                            <ChevronRight className="size-3.5 text-muted-foreground" />
                          )}
                          {b.displayName}
                        </span>
                        <span className={b.netBalance >= 0 ? 'text-emerald-600' : 'text-destructive'}>
                          {b.netBalance >= 0 ? `is owed $${b.netBalance.toFixed(2)}` : `owes $${Math.abs(b.netBalance).toFixed(2)}`}
                        </span>
                      </button>
                      {isExpanded && (
                        <ul className="flex flex-col gap-2 pb-3 pl-5 text-xs text-muted-foreground">
                          {breakdown.length === 0 ? (
                            <li>No expenses involve {b.displayName} yet.</li>
                          ) : (
                            breakdown.map((row) => (
                              <li key={row.expense.id} className="flex items-start justify-between gap-2">
                                <div>
                                  <p className="text-foreground">{row.expense.description}</p>
                                  <p>
                                    {row.paid > 0 && `paid $${row.paid.toFixed(2)}`}
                                    {row.paid > 0 && row.share > 0 && ' · '}
                                    {row.share > 0 && `share $${row.share.toFixed(2)}`}
                                  </p>
                                </div>
                                <span className={row.net >= 0 ? 'text-emerald-600' : 'text-destructive'}>
                                  {row.net >= 0 ? '+' : '-'}${Math.abs(row.net).toFixed(2)}
                                </span>
                              </li>
                            ))
                          )}
                        </ul>
                      )}
                    </li>
                  )
                })}
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
                    <div className="flex shrink-0 gap-1">
                      <Button asChild size="sm" variant="ghost">
                        <Link to={`/groups/${group.id}/expenses/${expense.id}/edit`}>Edit</Link>
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => void handleDeleteExpense(expense.id)}>
                        Delete
                      </Button>
                    </div>
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
