import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'

function Avatar({ initial }: { initial: string }) {
  return (
    <div className="flex size-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-medium text-primary">
      {initial}
    </div>
  )
}

// A static, illustrative recreation of the group detail + AI chat screens — not a live
// screenshot — so first-time visitors see the actual product shape (expenses, balances,
// minimum-transfer settlement, natural-language entry) before ever signing up.
function ProductPreview() {
  return (
    <div className="w-full max-w-md overflow-hidden rounded-xl border bg-card shadow-sm">
      <div className="flex items-center gap-1.5 border-b bg-muted/50 px-4 py-2.5">
        <span className="size-2.5 rounded-full bg-destructive/40" />
        <span className="size-2.5 rounded-full bg-amber-400/50" />
        <span className="size-2.5 rounded-full bg-primary/40" />
        <span className="ml-2 text-xs text-muted-foreground">Weekend Trip</span>
      </div>

      <div className="flex flex-col gap-4 p-4 text-left">
        <div>
          <p className="mb-2 text-xs font-medium text-muted-foreground">Expenses</p>
          <ul className="flex flex-col gap-2 text-sm">
            <li className="flex items-center justify-between">
              <span className="flex items-center gap-2">
                <Avatar initial="A" />
                Dinner
              </span>
              <span className="text-muted-foreground">$90.00</span>
            </li>
            <li className="flex items-center justify-between">
              <span className="flex items-center gap-2">
                <Avatar initial="Y" />
                Taxi
              </span>
              <span className="text-muted-foreground">$28.00</span>
            </li>
          </ul>
        </div>

        <div className="border-t pt-3">
          <p className="mb-2 text-xs font-medium text-muted-foreground">Balances</p>
          <ul className="flex flex-col gap-1 text-sm">
            <li className="flex items-center justify-between">
              <span className="flex items-center gap-2">
                <Avatar initial="A" />
                Alice
              </span>
              <span className="text-primary">is owed $12.50</span>
            </li>
            <li className="flex items-center justify-between">
              <span className="flex items-center gap-2">
                <Avatar initial="Y" />
                You
              </span>
              <span className="text-destructive">owe $12.50</span>
            </li>
          </ul>
          <p className="mt-2 rounded-md bg-muted px-2.5 py-1.5 text-xs text-muted-foreground">
            1 transfer settles it: You → Alice $12.50
          </p>
        </div>

        <div className="border-t pt-3">
          <p className="mb-2 text-xs font-medium text-muted-foreground">Settle up &amp; share</p>
          <pre className="whitespace-pre-wrap rounded bg-muted p-2.5 text-xs text-muted-foreground">
            {'Alice, you\'ll get $12.50 from You.\nAccount: Kiwibank 38-1234-5678900-00'}
          </pre>
          <div className="mt-2 flex gap-1.5">
            <span className="rounded-md border px-2 py-1 text-xs">Copy message</span>
            <span className="rounded-md border px-2 py-1 text-xs">Email</span>
            <span className="rounded-md border px-2 py-1 text-xs">WhatsApp</span>
          </div>
        </div>

        <div className="border-t pt-3">
          <p className="mb-2 text-xs font-medium text-muted-foreground">AI chat</p>
          <div className="flex flex-col gap-1.5">
            <p className="ml-auto max-w-[85%] rounded-lg bg-primary px-2.5 py-1.5 text-xs text-primary-foreground">
              I paid $28 for the taxi, split it with Alice
            </p>
            <p className="max-w-[85%] rounded-lg bg-muted px-2.5 py-1.5 text-xs text-muted-foreground">
              Got it — logged $14.00 each. Confirm?
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}

export function LandingPage() {
  return (
    <div className="mx-auto grid max-w-5xl grid-cols-1 items-center gap-12 py-16 md:grid-cols-2">
      <div className="flex flex-col items-center gap-6 text-center md:items-start md:text-left">
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

      <div className="flex justify-center">
        <ProductPreview />
      </div>
    </div>
  )
}
