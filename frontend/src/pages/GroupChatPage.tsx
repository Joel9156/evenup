import { useEffect, useState, type SubmitEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useMyMemberId } from '@/hooks/useMyMemberId'
import { ApiError, apiFetch } from '@/lib/api'
import type { AiChatMessageDto, AiChatResponse, ExpenseSuggestion, GroupResponse } from '@/lib/types'

interface ShareState {
  included: boolean
  amount: string
}

interface EditableExpense {
  description: string
  totalAmount: string
  paidByMemberId: string
  shares: Record<string, ShareState>
  editingExpenseId: string | null
}

function summarize(suggestion: ExpenseSuggestion): string {
  const verb = suggestion.editingExpenseId ? "I'll update" : "I'll log"
  const shares = suggestion.shares.map((s) => `${s.displayName} $${s.amount.toFixed(2)}`).join(', ')
  return `${verb} "${suggestion.description}" — $${suggestion.totalAmount.toFixed(2)}, paid by ${suggestion.paidByDisplayName}, split: ${shares}`
}

// The AI's suggestion is a starting point, not gospel — compound instructions occasionally
// trip it up (wrong split math, wrong idea of who's included). Seeding an editable form from
// it, rather than only offering Confirm/Discard on the raw numbers, means a wrong guess is a
// quick fix instead of a trip back through the chat.
function buildEditableExpense(suggestion: ExpenseSuggestion, group: GroupResponse): EditableExpense {
  const sharesByMemberId = new Map(suggestion.shares.map((s) => [s.memberId, s.amount]))

  return {
    description: suggestion.description,
    totalAmount: suggestion.totalAmount.toFixed(2),
    paidByMemberId: suggestion.paidByMemberId,
    editingExpenseId: suggestion.editingExpenseId,
    shares: Object.fromEntries(
      group.members.map((m) => [
        m.id,
        sharesByMemberId.has(m.id)
          ? { included: true, amount: sharesByMemberId.get(m.id)!.toFixed(2) }
          : { included: false, amount: '' },
      ]),
    ),
  }
}

export function GroupChatPage() {
  const { id } = useParams<{ id: string }>()
  const [group, setGroup] = useState<GroupResponse | null>(null)
  const [conversation, setConversation] = useState<AiChatMessageDto[]>([])
  const [editableExpense, setEditableExpense] = useState<EditableExpense | null>(null)
  const [input, setInput] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSending, setIsSending] = useState(false)

  const myMemberId = useMyMemberId(group, id)

  useEffect(() => {
    if (!id) return
    apiFetch<GroupResponse>(`/api/groups/${id}`).then(setGroup).catch(() => setError('Failed to load this group.'))
  }, [id])

  async function handleSend(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!input.trim() || !id) return

    const nextConversation: AiChatMessageDto[] = [...conversation, { role: 'user', content: input.trim() }]
    setConversation(nextConversation)
    setInput('')
    setEditableExpense(null)
    setError(null)
    setIsSending(true)

    try {
      const response = await apiFetch<AiChatResponse>(`/api/groups/${id}/ai-chat`, {
        method: 'POST',
        body: { messages: nextConversation },
      })

      // Members the AI added are already saved (not staged like an expense suggestion) — reload
      // the group so a newly added person shows up in the confirm card's paid-by/split options
      // if this same turn also logged an expense involving them.
      let currentGroup = group
      if (response.addedMembers.length > 0 && id) {
        currentGroup = await apiFetch<GroupResponse>(`/api/groups/${id}`)
        setGroup(currentGroup)
      }

      const newTurns: AiChatMessageDto[] = []
      if (response.addedMembers.length > 0) {
        newTurns.push({ role: 'assistant', content: `Added ${response.addedMembers.join(', ')} to the group.` })
      }
      if (response.needsClarification) {
        newTurns.push({ role: 'assistant', content: response.clarificationQuestion ?? 'Could you clarify that?' })
      } else if (response.suggestion && currentGroup) {
        newTurns.push({ role: 'assistant', content: summarize(response.suggestion) })
        setEditableExpense(buildEditableExpense(response.suggestion, currentGroup))
      }
      if (newTurns.length > 0) {
        setConversation((prev) => [...prev, ...newTurns])
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong talking to the AI.')
    } finally {
      setIsSending(false)
    }
  }

  function toggleIncluded(memberId: string) {
    setEditableExpense((prev) =>
      prev ? { ...prev, shares: { ...prev.shares, [memberId]: { ...prev.shares[memberId], included: !prev.shares[memberId].included } } } : prev,
    )
  }

  function setShareAmount(memberId: string, amount: string) {
    setEditableExpense((prev) =>
      prev ? { ...prev, shares: { ...prev.shares, [memberId]: { ...prev.shares[memberId], amount } } } : prev,
    )
  }

  function handleSplitEvenly() {
    setEditableExpense((prev) => {
      if (!prev) return prev
      const includedIds = Object.entries(prev.shares)
        .filter(([, s]) => s.included)
        .map(([memberId]) => memberId)
      const total = Number(prev.totalAmount)
      if (!total || includedIds.length === 0) return prev

      const evenShare = (total / includedIds.length).toFixed(2)
      const nextShares = { ...prev.shares }
      for (const memberId of includedIds) {
        nextShares[memberId] = { ...nextShares[memberId], amount: evenShare }
      }
      return { ...prev, shares: nextShares }
    })
  }

  async function handleConfirm() {
    if (!editableExpense || !myMemberId || !id) return
    setError(null)
    setIsSending(true)

    const shares = Object.entries(editableExpense.shares)
      .filter(([, s]) => s.included && Number(s.amount) > 0)
      .map(([memberId, s]) => ({ memberId, amount: Number(s.amount) }))

    try {
      if (editableExpense.editingExpenseId) {
        await apiFetch(`/api/expenses/${editableExpense.editingExpenseId}`, {
          method: 'PUT',
          body: {
            description: editableExpense.description,
            totalAmount: Number(editableExpense.totalAmount),
            paidByMemberId: editableExpense.paidByMemberId,
            shares,
          },
        })
      } else {
        await apiFetch(`/api/groups/${id}/expenses`, {
          method: 'POST',
          body: {
            description: editableExpense.description,
            totalAmount: Number(editableExpense.totalAmount),
            paidByMemberId: editableExpense.paidByMemberId,
            createdByMemberId: myMemberId,
            shares,
          },
        })
      }
      setEditableExpense(null)
      setConversation((prev) => [...prev, { role: 'assistant', content: 'Saved! What else would you like to log?' }])
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save this expense.')
    } finally {
      setIsSending(false)
    }
  }

  return (
    <div className="mx-auto flex max-w-md flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">AI chat</h1>
        {id && (
          <Button asChild size="sm" variant="outline">
            <Link to={`/groups/${id}`}>Back to {group?.name ?? 'group'}</Link>
          </Button>
        )}
      </div>

      <div className="flex flex-col gap-2">
        {conversation.map((turn, i) => (
          <div
            key={i}
            className={`max-w-[85%] rounded-lg px-3 py-2 text-sm ${
              turn.role === 'user' ? 'ml-auto bg-primary text-primary-foreground' : 'bg-muted'
            }`}
          >
            {turn.content}
          </div>
        ))}
        {conversation.length === 0 && (
          <p className="text-sm text-muted-foreground">
            Try something like "I spent $90 on dinner tonight, split evenly between everyone".
          </p>
        )}
      </div>

      {editableExpense && group && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {editableExpense.editingExpenseId ? 'Confirm this update' : 'Confirm this expense'}
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-xs text-muted-foreground">
              The AI's best guess — check the numbers and fix anything before confirming.
            </p>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="chatDescription">Description</Label>
              <Input
                id="chatDescription"
                value={editableExpense.description}
                onChange={(e) => setEditableExpense((prev) => (prev ? { ...prev, description: e.target.value } : prev))}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="chatTotalAmount">Total amount</Label>
              <Input
                id="chatTotalAmount"
                type="number"
                min="0.01"
                step="0.01"
                value={editableExpense.totalAmount}
                onChange={(e) => setEditableExpense((prev) => (prev ? { ...prev, totalAmount: e.target.value } : prev))}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="chatPaidBy">Paid by</Label>
              <select
                id="chatPaidBy"
                className="h-8 rounded-lg border border-input bg-background px-2.5 text-sm"
                value={editableExpense.paidByMemberId}
                onChange={(e) => setEditableExpense((prev) => (prev ? { ...prev, paidByMemberId: e.target.value } : prev))}
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
                    checked={editableExpense.shares[member.id]?.included ?? false}
                    onChange={() => toggleIncluded(member.id)}
                    className="size-4"
                  />
                  <span className="flex-1 text-sm">{member.displayName}</span>
                  <Input
                    type="number"
                    min="0"
                    step="0.01"
                    className="w-24"
                    disabled={!editableExpense.shares[member.id]?.included}
                    value={editableExpense.shares[member.id]?.amount ?? ''}
                    onChange={(e) => setShareAmount(member.id, e.target.value)}
                  />
                </div>
              ))}
            </div>

            <div className="flex gap-2">
              <Button size="sm" onClick={() => void handleConfirm()} disabled={isSending || !myMemberId}>
                Confirm
              </Button>
              <Button size="sm" variant="outline" onClick={() => setEditableExpense(null)}>
                Discard
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {error && <p className="text-sm text-destructive">{error}</p>}

      <form onSubmit={handleSend} className="flex gap-2">
        <Input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Describe an expense..."
          disabled={isSending}
        />
        <Button type="submit" disabled={isSending || !input.trim()}>
          {isSending ? '...' : 'Send'}
        </Button>
      </form>
    </div>
  )
}
