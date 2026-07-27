import { useParams } from 'react-router-dom'

export function GroupSettlePage() {
  const { id } = useParams()

  return (
    <div>
      <h1 className="text-2xl font-semibold">Settle up</h1>
      <p className="mt-2 text-muted-foreground">Group {id} — coming soon.</p>
    </div>
  )
}
