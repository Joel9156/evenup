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

export interface GroupPreviewResponse {
  groupId: string
  groupName: string
  memberNames: string[]
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
