export interface MemberResponse {
  id: string
  userId: string | null
  displayName: string
  isGuest: boolean
  joinedAt: string
}

export interface GroupResponse {
  id: string
  name: string
  inviteCode: string
  createdAt: string
  members: MemberResponse[]
}

export interface PreviewMemberResponse {
  id: string
  displayName: string
}

export interface GroupPreviewResponse {
  groupId: string
  groupName: string
  memberNames: string[]
  claimableMembers: PreviewMemberResponse[]
}

export interface JoinGroupResponse {
  memberId: string
  groupId: string
  displayName: string
  isGuest: boolean
}

export interface ExpenseShareResponse {
  memberId: string
  memberDisplayName: string
  amount: number
}

export interface ExpenseResponse {
  id: string
  groupId: string
  description: string
  totalAmount: number
  paidByMemberId: string
  paidByDisplayName: string
  createdByMemberId: string
  createdAt: string
  updatedAt: string | null
  shares: ExpenseShareResponse[]
}

export interface MemberBalanceResponse {
  memberId: string
  displayName: string
  netBalance: number
}

export interface SettlementTransactionResponse {
  fromMemberId: string
  fromDisplayName: string
  toMemberId: string
  toDisplayName: string
  amount: number
}

export interface BalancesResponse {
  netBalances: MemberBalanceResponse[]
  suggestedTransactions: SettlementTransactionResponse[]
}

export interface SettleResponse {
  settlementId: string
  generatedAt: string
  transactions: SettlementTransactionResponse[]
}

export interface SettlementMessageResponse {
  fromMemberId: string
  fromDisplayName: string
  toMemberId: string
  toDisplayName: string
  amount: number
  accountInfoProvided: boolean
  messageText: string
  mailtoLink: string
  whatsAppLink: string
}

export interface MeResponse {
  id: string
  email: string
  displayName: string
  bankName: string | null
  hasAccountNumber: boolean
  createdAt: string
}

export interface UpdateAccountResponse {
  bankName: string
  maskedAccountNumber: string
}

export type AiChatRole = 'user' | 'assistant'

export interface AiChatMessageDto {
  role: AiChatRole
  content: string
}

export interface ExpenseShareSuggestion {
  memberId: string
  displayName: string
  amount: number
}

export interface ExpenseSuggestion {
  description: string
  totalAmount: number
  paidByMemberId: string
  paidByDisplayName: string
  shares: ExpenseShareSuggestion[]
}

export interface AiChatResponse {
  needsClarification: boolean
  clarificationQuestion: string | null
  suggestion: ExpenseSuggestion | null
}
