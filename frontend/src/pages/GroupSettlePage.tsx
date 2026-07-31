import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError, apiFetch } from '@/lib/api'
import type { GroupResponse, SettlementMessageResponse } from '@/lib/types'

interface AccountOverrideInput {
  bankName: string
  accountNumber: string
}

export function GroupSettlePage() {
  const { id } = useParams<{ id: string }>()
  const [searchParams] = useSearchParams()
  const settlementIdFromUrl = searchParams.get('settlementId')
  const [groupName, setGroupName] = useState<string | null>(null)
  const [settlementId, setSettlementId] = useState<string | null>(null)
  const [messages, setMessages] = useState<SettlementMessageResponse[] | null>(null)
  const [overrides, setOverrides] = useState<Record<string, AccountOverrideInput>>({})
  const [error, setError] = useState<string | null>(null)
  const [isSettling, setIsSettling] = useState(false)
  const [copiedFor, setCopiedFor] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    apiFetch<GroupResponse>(`/api/groups/${id}`)
      .then((g) => setGroupName(g.name))
      .catch(() => setGroupName(null))
  }, [id])

  useEffect(() => {
    if (!settlementIdFromUrl) return
    setSettlementId(settlementIdFromUrl)
    fetchMessages(settlementIdFromUrl, {}).catch((err) => {
      setError(err instanceof ApiError ? err.message : 'Could not load that settlement.')
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settlementIdFromUrl])

  async function fetchMessages(sid: string, currentOverrides: Record<string, AccountOverrideInput>) {
    const body = {
      accountOverrides: Object.entries(currentOverrides)
        .filter(([, v]) => v.bankName && v.accountNumber)
        .map(([memberId, v]) => ({ memberId, bankName: v.bankName, accountNumber: v.accountNumber })),
    }
    const result = await apiFetch<SettlementMessageResponse[]>(`/api/settlements/${sid}/messages`, {
      method: 'POST',
      body,
    })
    setMessages(result)
  }

  async function handleSettle() {
    if (!id) return
    setError(null)
    setIsSettling(true)

    try {
      const result = await apiFetch<{ settlementId: string }>(`/api/groups/${id}/settle`, { method: 'POST' })
      setSettlementId(result.settlementId)
      await fetchMessages(result.settlementId, overrides)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to settle up.')
    } finally {
      setIsSettling(false)
    }
  }

  async function handleUpdateMessages() {
    if (!settlementId) return
    await fetchMessages(settlementId, overrides)
  }

  function updateOverride(memberId: string, field: keyof AccountOverrideInput, value: string) {
    setOverrides((prev) => {
      const current = prev[memberId] ?? { bankName: '', accountNumber: '' }
      return { ...prev, [memberId]: { ...current, [field]: value } }
    })
  }

  function handleCopy(key: string, text: string) {
    navigator.clipboard.writeText(text)
    setCopiedFor(key)
    setTimeout(() => setCopiedFor(null), 2000)
  }

  return (
    <div className="mx-auto flex max-w-md flex-col gap-4">
      {id && (
        <Link to={`/groups/${id}`} className="text-sm text-muted-foreground hover:text-foreground">
          ← Back to {groupName ?? 'group'}
        </Link>
      )}
      <h1 className="text-2xl font-semibold">Settle up</h1>

      {!settlementId && (
        <Card>
          <CardContent className="flex flex-col gap-3 pt-4">
            <p className="text-sm text-muted-foreground">
              This calculates the minimum number of transfers needed to settle every balance in
              this group right now.
            </p>
            {error && <p className="text-sm text-destructive">{error}</p>}
            <Button onClick={() => void handleSettle()} disabled={isSettling}>
              {isSettling ? 'Settling...' : 'Settle up now'}
            </Button>
          </CardContent>
        </Card>
      )}

      {settlementId && error && <p className="text-sm text-destructive">{error}</p>}

      {settlementId && messages?.length === 0 && (
        <p className="text-sm text-muted-foreground">Everyone's already settled up — no transfers needed.</p>
      )}

      {messages?.map((message) => {
        const key = `${message.fromMemberId}-${message.toMemberId}`
        return (
          <Card key={key}>
            <CardHeader>
              <CardTitle className="text-base">
                {message.fromDisplayName} → {message.toDisplayName}: ${message.amount.toFixed(2)}
              </CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-3">
              <pre className="whitespace-pre-wrap rounded bg-muted p-3 text-xs">{message.messageText}</pre>

              {!message.accountInfoProvided && (
                <div className="flex flex-col gap-2 rounded border border-dashed p-3">
                  <p className="text-xs text-muted-foreground">
                    {message.toDisplayName} hasn't registered a bank account — enter it here to include it in the
                    message:
                  </p>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor={`bank-${key}`}>Bank name</Label>
                    <Input
                      id={`bank-${key}`}
                      value={overrides[message.toMemberId]?.bankName ?? ''}
                      onChange={(e) => updateOverride(message.toMemberId, 'bankName', e.target.value)}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor={`account-${key}`}>Account number</Label>
                    <Input
                      id={`account-${key}`}
                      value={overrides[message.toMemberId]?.accountNumber ?? ''}
                      onChange={(e) => updateOverride(message.toMemberId, 'accountNumber', e.target.value)}
                    />
                  </div>
                  <Button size="sm" variant="outline" onClick={() => void handleUpdateMessages()}>
                    Update message
                  </Button>
                </div>
              )}

              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" onClick={() => handleCopy(key, message.messageText)}>
                  {copiedFor === key ? 'Copied!' : 'Copy message'}
                </Button>
                <Button asChild size="sm" variant="outline">
                  <a href={message.mailtoLink}>Email</a>
                </Button>
                <Button asChild size="sm" variant="outline">
                  <a href={message.whatsAppLink} target="_blank" rel="noreferrer">
                    WhatsApp
                  </a>
                </Button>
              </div>
            </CardContent>
          </Card>
        )
      })}
    </div>
  )
}
