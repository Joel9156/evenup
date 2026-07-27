import { useEffect, useState, type SubmitEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError, apiFetch } from '@/lib/api'
import type { MeResponse, UpdateAccountResponse } from '@/lib/types'

export function ProfilePage() {
  const [me, setMe] = useState<MeResponse | null>(null)
  const [bankName, setBankName] = useState('')
  const [accountNumber, setAccountNumber] = useState('')
  const [savedMasked, setSavedMasked] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    apiFetch<MeResponse>('/api/auth/me').then(setMe).catch(() => setError('Failed to load your profile.'))
  }, [])

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSaving(true)

    try {
      const result = await apiFetch<UpdateAccountResponse>('/api/auth/me/account', {
        method: 'PUT',
        body: { bankName, accountNumber },
      })
      setSavedMasked(result.maskedAccountNumber)
      setMe((prev) => (prev ? { ...prev, bankName: result.bankName, hasAccountNumber: true } : prev))
      setAccountNumber('')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save your account details.')
    } finally {
      setIsSaving(false)
    }
  }

  if (!me) {
    return <p className="text-muted-foreground">{error ?? 'Loading...'}</p>
  }

  return (
    <div className="mx-auto flex max-w-sm flex-col gap-4">
      <h1 className="text-2xl font-semibold">Profile</h1>

      <Card>
        <CardHeader>
          <CardTitle>Account</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-1 text-sm text-muted-foreground">
          <p>{me.displayName}</p>
          <p>{me.email}</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Bank account</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">
            {me.hasAccountNumber
              ? `Registered: ${me.bankName}${savedMasked ? ` ${savedMasked}` : ''}`
              : "You haven't registered a bank account yet — this is what shows up when someone else settles up with you."}
          </p>

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="bankName">Bank name</Label>
              <Input
                id="bankName"
                value={bankName}
                onChange={(e) => setBankName(e.target.value)}
                placeholder={me.bankName ?? ''}
                required
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="accountNumber">Account number</Label>
              <Input
                id="accountNumber"
                value={accountNumber}
                onChange={(e) => setAccountNumber(e.target.value)}
                required
              />
            </div>
            {error && <p className="text-sm text-destructive">{error}</p>}
            <Button type="submit" disabled={isSaving}>
              {isSaving ? 'Saving...' : me.hasAccountNumber ? 'Update account' : 'Save account'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
