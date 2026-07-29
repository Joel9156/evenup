import { Monitor, Moon, Sun } from 'lucide-react'
import { Link, Outlet, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/authStore'
import { type Theme, useThemeStore } from '@/stores/themeStore'

const THEME_CYCLE: Record<Theme, Theme> = { light: 'dark', dark: 'system', system: 'light' }
const THEME_ICON: Record<Theme, typeof Sun> = { light: Sun, dark: Moon, system: Monitor }
const THEME_LABEL: Record<Theme, string> = { light: 'Light theme', dark: 'Dark theme', system: 'System theme' }

function ThemeToggle() {
  const { theme, setTheme } = useThemeStore()
  const Icon = THEME_ICON[theme]

  return (
    <Button
      variant="ghost"
      size="icon-sm"
      onClick={() => setTheme(THEME_CYCLE[theme])}
      aria-label={`${THEME_LABEL[theme]} — click to change`}
      title={THEME_LABEL[theme]}
    >
      <Icon className="size-4" />
    </Button>
  )
}

export function Layout() {
  const { user, logout } = useAuthStore()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login')
  }

  return (
    <div className="flex min-h-svh flex-col">
      <header className="border-border border-b">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <Link to={user ? '/dashboard' : '/'} className="font-semibold">
            EvenUp
          </Link>
          <nav className="flex items-center gap-3 text-sm">
            {user ? (
              <>
                <Link to="/profile" className="text-muted-foreground hover:text-foreground">
                  {user.displayName}
                </Link>
                <Button variant="outline" size="sm" onClick={handleLogout}>
                  Log out
                </Button>
              </>
            ) : (
              <>
                <Link to="/login" className="text-muted-foreground hover:text-foreground">
                  Log in
                </Link>
                <Button asChild size="sm">
                  <Link to="/register">Sign up</Link>
                </Button>
              </>
            )}
            <ThemeToggle />
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
