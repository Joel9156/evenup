export interface MemberResponse {
  id: string
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
