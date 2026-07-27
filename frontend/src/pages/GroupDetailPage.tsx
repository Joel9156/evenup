import { useParams } from 'react-router-dom'

export function GroupDetailPage() {
  const { id } = useParams()

  return (
    <div>
      <h1 className="text-2xl font-semibold">Group {id}</h1>
      <p className="mt-2 text-muted-foreground">Coming soon.</p>
    </div>
  )
}
