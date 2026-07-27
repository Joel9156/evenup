import { useEffect, useState, type SubmitEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useMyMemberId } from '@/hooks/useMyMemberId'
import { ApiError, apiFetch } from '@/lib/api'
import type { AiChatMessageDto, AiChatResponse, ExpenseSuggestion, GroupResponse } from '@/lib/types'

function summarize(suggestion: ExpenseSuggestion): string {
  const shares = suggestion.shares.map((s) => `${s.displayName} $${s.amount.toFixed(2)}`).join(', ')
  return `I'll log "${suggestion.description}" — $${suggestion.totalAmount.toFixed(2)}, paid by ${suggestion.paidByDisplayName}, split: ${shares}`
}

export function GroupChatPage() {
  const { id } = useParams<{ id: string }>()
  const [group, setGroup] = useState<GroupResponse | null>(null)
  const [conversation, setConversation] = useState<AiChatMessageDto[]>([])
  const [suggestion, setSuggestion] = useState<ExpenseSuggestion | null>(null)
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
    setSuggestion(null)
    setError(null)
    setIsSending(true)

    try {
      const response = await apiFetch<AiChatResponse>(`/api/groups/${id}/ai-chat`, {
        method: 'POST',
        body: { messages: nextConversation },
      })

      if (response.needsClarification) {
        setConversation((prev) => [
          ...prev,
          { role: 'assistant', content: response.clarificationQuestion ?? 'Could you clarify that?' },
        ])
      } else if (response.suggestion) {
        setConversation((prev) => [...prev, { role: 'assistant', content: summarize(response.suggestion!) }])
        setSuggestion(response.suggestion)
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong talking to the AI.')
    } finally {
      setIsSending(false)
    }
  }

  async function handleConfirm() {
    if (!suggestion || !myMemberId || !id) return
    setError(null)
    setIsSending(true)

    try {
      await apiFetch(`/api/groups/${id}/expenses`, {
        method: 'POST',
        body: {
          description: suggestion.description,
          totalAmount: suggestion.totalAmount,
          paidByMemberId: suggestion.paidByMemberId,
          createdByMemberId: myMemberId,
          shares: suggestion.shares.map((s) => ({ memberId: s.memberId, amount: s.amount })),
        },
      })
      setSuggestion(null)
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
            <Link to={`/groups/${id}`}>Back to group</Link>
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

      {suggestion && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Confirm this expense?</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <div className="text-sm">
              <p className="font-medium">{suggestion.description}</p>
              <p className="text-muted-foreground">
                ${suggestion.totalAmount.toFixed(2)} paid by {suggestion.paidByDisplayName}
              </p>
              <ul className="mt-1 text-muted-foreground">
                {suggestion.shares.map((s) => (
                  <li key={s.memberId}>
                    {s.displayName}: ${s.amount.toFixed(2)}
                  </li>
                ))}
              </ul>
            </div>
            <div className="flex gap-2">
              <Button size="sm" onClick={() => void handleConfirm()} disabled={isSending || !myMemberId}>
                Confirm
              </Button>
              <Button size="sm" variant="outline" onClick={() => setSuggestion(null)}>
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
