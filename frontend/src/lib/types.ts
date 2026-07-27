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
