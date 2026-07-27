import { useParams } from 'react-router-dom'

export function NewExpensePage() {
  const { id } = useParams()

  return (
    <div>
      <h1 className="text-2xl font-semibold">Add an expense</h1>
      <p className="mt-2 text-muted-foreground">Group {id} — coming soon.</p>
    </div>
  )
}
