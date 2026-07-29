import { create } from 'zustand'

export type Theme = 'light' | 'dark' | 'system'

const STORAGE_KEY = 'evenup-theme'

function systemPrefersDark(): boolean {
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

function isDark(theme: Theme): boolean {
  return theme === 'dark' || (theme === 'system' && systemPrefersDark())
}

function applyTheme(theme: Theme) {
  document.documentElement.classList.toggle('dark', isDark(theme))
}

interface ThemeState {
  theme: Theme
  setTheme: (theme: Theme) => void
}

// Deliberately not using zustand's persist middleware: the value needs to be readable by a
// plain synchronous script in index.html (see there) before React even loads, to set the
// `dark` class pre-paint and avoid a flash of the wrong theme. persist's JSON-wrapped storage
// format would require that script to know zustand's internals just to read one string.
export const useThemeStore = create<ThemeState>((set) => ({
  theme: (localStorage.getItem(STORAGE_KEY) as Theme | null) ?? 'system',
  setTheme: (theme) => {
    localStorage.setItem(STORAGE_KEY, theme)
    applyTheme(theme)
    set({ theme })
  },
}))

// Keep the resolved theme in sync with OS-level changes while "system" is selected.
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
  if (useThemeStore.getState().theme === 'system') {
    applyTheme('system')
  }
})
