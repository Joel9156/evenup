import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'

export function LandingPage() {
  return (
    <div className="flex flex-col items-center gap-6 py-16 text-center">
      <h1 className="text-3xl font-semibold">Split expenses without the awkward math</h1>
      <p className="max-w-md text-muted-foreground">
        Track group expenses, settle up with the fewest transfers possible, and let friends
        join without even needing an account.
      </p>
      <div className="flex gap-3">
        <Button asChild>
          <Link to="/register">Get started</Link>
        </Button>
        <Button asChild variant="outline">
          <Link to="/login">Log in</Link>
        </Button>
      </div>
    </div>
  )
}
