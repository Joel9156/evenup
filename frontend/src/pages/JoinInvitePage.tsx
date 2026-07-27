import { useParams } from 'react-router-dom'

export function JoinInvitePage() {
  const { inviteCode } = useParams()

  return (
    <div>
      <h1 className="text-2xl font-semibold">Join a group</h1>
      <p className="mt-2 text-muted-foreground">Invite code {inviteCode} — coming soon.</p>
    </div>
  )
}
