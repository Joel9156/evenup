import { createBrowserRouter } from 'react-router-dom'
import { Layout } from '@/components/Layout'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { DashboardPage } from '@/pages/DashboardPage'
import { EditExpensePage } from '@/pages/EditExpensePage'
import { GroupChatPage } from '@/pages/GroupChatPage'
import { GroupDetailPage } from '@/pages/GroupDetailPage'
import { GroupSettlePage } from '@/pages/GroupSettlePage'
import { JoinInvitePage } from '@/pages/JoinInvitePage'
import { LandingPage } from '@/pages/LandingPage'
import { LoginPage } from '@/pages/LoginPage'
import { NewExpensePage } from '@/pages/NewExpensePage'
import { NewGroupPage } from '@/pages/NewGroupPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { ProfilePage } from '@/pages/ProfilePage'
import { RegisterPage } from '@/pages/RegisterPage'

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <LandingPage /> },
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
      { path: '/join/:inviteCode', element: <JoinInvitePage /> },
      // Not sign-in gated: guests interact with these without ever having an account.
      { path: '/groups/:id', element: <GroupDetailPage /> },
      { path: '/groups/:id/expenses/new', element: <NewExpensePage /> },
      { path: '/groups/:id/settle', element: <GroupSettlePage /> },
      {
        element: <ProtectedRoute />,
        children: [
          { path: '/dashboard', element: <DashboardPage /> },
          { path: '/groups/new', element: <NewGroupPage /> },
          { path: '/groups/:id/chat', element: <GroupChatPage /> },
          { path: '/groups/:id/expenses/:expenseId/edit', element: <EditExpensePage /> },
          { path: '/profile', element: <ProfilePage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
