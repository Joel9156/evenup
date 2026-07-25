# 프로젝트 스펙: 정산 친구 (가칭)

> 이 문서는 Claude Code에 그대로 전달해서 개발을 시작할 수 있도록 작성된 상세 스펙입니다.
> 포트폴리오(CV 첨부용) 프로젝트 — 웹사이트 UI 언어는 영어.

---

## 0. 결정 로그 (Decisions)

| 항목 | 결정 |
|---|---|
| AI 연동 | **OpenAI API 직접 호출** (Azure OpenAI 아님 — 설정 간단, 개인 프로젝트 규모에 적합) |
| UI 스타일링 | **Tailwind CSS + shadcn/ui** |
| 테스트 범위 | **핵심 로직 단위테스트만** (BE: 정산 알고리즘, 권한 체크 — xUnit). FE 컴포넌트 테스트/E2E는 범위 밖 |
| 라이브 데모 배포 | **함** — Frontend: Vercel / Backend: Render 또는 Railway (무료 티어) / DB(프로덕션): Neon Postgres, 로컬 개발은 SQLite 유지 |
| Azure | 이 프로젝트에서는 강제하지 않음 (다른 프로젝트인 Green Footprint와의 스택 정렬 목적이었을 뿐, 배포 플랫폼은 자유롭게 선택) |

> DB 프로바이더는 EF Core 사용이므로 로컬(SQLite) ↔ 배포(Postgres) 전환이 `UseSqlite` / `UseNpgsql` 교체 + 마이그레이션 재생성만으로 가능.

---

## 1. 프로젝트 개요

**한 줄 요약**: 그룹 지출을 기록하고, 로그인 없이 게스트로 참여 가능하며, AI 챗으로 자연어로 지출을 입력할 수 있는 정산(더치페이) 웹사이트.

**타겟 시나리오**:
- 단발성: 여러 명이 밥 먹고 바로 정산
- 지속형: 여행/룸메이트처럼 여러 건의 지출이 쌓였다가 나중에 한 번에 정산

**차별점**:
- 여러 건이 얽혀도 **최소 송금 횟수**로 자동 정리 (누가 누구에게 여러 번 보낼 필요 없음)
- **AI 챗**으로 "오늘 저녁 90불 썼는데 나 빼고 셋이서 20불 덜" 같은 자연어를 구조화된 지출 데이터로 변환
- **게스트 참여**: 초대받은 사람이 회원가입 없이 이름만으로 참여 가능

---

## 2. 기술 스택

| 영역 | 기술 |
|---|---|
| Frontend | React + TypeScript, Vite |
| UI/스타일링 | Tailwind CSS + shadcn/ui |
| Backend | ASP.NET Core Web API (C#, .NET 10) |
| ORM | Entity Framework Core |
| DB (로컬/개발) | SQLite |
| DB (프로덕션/데모) | Neon Postgres (무료 티어) |
| 인증 | JWT (sign-in 유저만) |
| AI | OpenAI API (Chat Completions, Function Calling / Tool Use) |
| API 문서 | Scalar |
| 배포 (Frontend) | Vercel |
| 배포 (Backend) | Render 또는 Railway (무료 티어) |
| 상태관리 | Zustand (선택) |
| 테스트 | xUnit (BE 핵심 로직만) |

---

## 3. 권한 구조

| 기능 | Sign-in 유저 | Guest |
|---|---|---|
| 그룹 생성 | ✅ (그룹 생성자는 항상 sign-in 유저) | ❌ |
| 초대 링크로 그룹 참여 | ✅ | ✅ |
| 본인 지출 입력 | ✅ | ✅ |
| 계좌번호 | 프로필에 저장된 계좌 사용 (암호화 저장) | 정산 메시지 생성 시 그때그때 직접 입력 (DB 미저장) |
| 지출 편집/삭제 | ✅ (본인이 작성한 것) | ❌ |
| AI 챗 | ✅ | ❌ |
| 정산 메시지 생성 및 공유 | ✅ | ✅ |

**핵심 규칙**: 그룹당 최소 1명은 sign-in 유저(= 그룹 생성자). 그룹 생성 API 자체를 sign-in 유저만 호출 가능하도록 제한하면 자동으로 충족됨.

---

## 4. DB 스키마

```sql
-- 유저 (sign-in 유저)
CREATE TABLE Users (
    Id UUID PRIMARY KEY,
    Email TEXT UNIQUE NOT NULL,
    PasswordHash TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    AccountNumberEncrypted TEXT NULL,   -- AES 등으로 양방향 암호화 (해싱 아님, 나중에 복호화해서 보여줘야 함)
    BankName TEXT NULL,
    CreatedAt DATETIME NOT NULL
);

-- 그룹
CREATE TABLE Groups (
    Id UUID PRIMARY KEY,
    Name TEXT NOT NULL,
    InviteCode TEXT UNIQUE NOT NULL,     -- 짧은 랜덤 코드, 초대 링크에 사용
    CreatedByUserId UUID NOT NULL REFERENCES Users(Id),
    CreatedAt DATETIME NOT NULL
);

-- 그룹 멤버 (sign-in 유저 또는 guest)
CREATE TABLE Members (
    Id UUID PRIMARY KEY,
    GroupId UUID NOT NULL REFERENCES Groups(Id),
    UserId UUID NULL REFERENCES Users(Id),   -- guest면 NULL
    DisplayName TEXT NOT NULL,
    IsGuest BOOLEAN NOT NULL,
    JoinedAt DATETIME NOT NULL
);

-- 지출 항목
CREATE TABLE Expenses (
    Id UUID PRIMARY KEY,
    GroupId UUID NOT NULL REFERENCES Groups(Id),
    PaidByMemberId UUID NOT NULL REFERENCES Members(Id),
    CreatedByMemberId UUID NOT NULL REFERENCES Members(Id),  -- 편집 권한 체크용
    Description TEXT NOT NULL,
    TotalAmount DECIMAL(10,2) NOT NULL,
    CreatedAt DATETIME NOT NULL,
    UpdatedAt DATETIME NULL
);

-- 지출 항목별 분배 (누가 얼마씩 부담하는지)
CREATE TABLE ExpenseShares (
    Id UUID PRIMARY KEY,
    ExpenseId UUID NOT NULL REFERENCES Expenses(Id),
    MemberId UUID NOT NULL REFERENCES Members(Id),
    ShareAmount DECIMAL(10,2) NOT NULL
);

-- 정산 실행 기록 (언제, 어떤 방식으로 정산했는지 로그)
CREATE TABLE Settlements (
    Id UUID PRIMARY KEY,
    GroupId UUID NOT NULL REFERENCES Groups(Id),
    GeneratedAt DATETIME NOT NULL,
    SnapshotJson TEXT NOT NULL   -- 그 시점의 송금 계획(누가 누구에게 얼마)을 JSON으로 저장
);
```

**설계 노트**:
- `AccountNumberEncrypted`는 해싱이 아니라 **암호화**(예: AES-256)를 씀. 비밀번호는 검증만 하면 되니 해싱(단방향)이지만, 계좌번호는 나중에 다시 보여줘야 하는 암호화(양방향)가 맞음. README에 이 차이를 설명하면 좋은 이해 포인트가 됨.
- Guest의 계좌번호는 테이블에 아예 컬럼이 없음 → DB에 저장하지 않고 정산 메시지 생성 화면에서만 입력받아 즉시 사용. 민감정보 최소 저장 원칙.

---

## 5. API 엔드포인트

### Auth
| Method | Endpoint | 설명 | 권한 |
|---|---|---|---|
| POST | `/api/auth/register` | 회원가입 | Public |
| POST | `/api/auth/login` | 로그인, JWT 발급 | Public |
| GET | `/api/auth/me` | 내 정보 조회 | Sign-in |
| PUT | `/api/auth/me/account` | 계좌번호 등록/수정 (암호화 저장) | Sign-in |

### Groups
| Method | Endpoint | 설명 | 권한 |
|---|---|---|---|
| POST | `/api/groups` | 그룹 생성 | Sign-in |
| GET | `/api/groups/{id}` | 그룹 정보 + 멤버 목록 조회 | 그룹 멤버 |
| GET | `/api/groups/join/{inviteCode}` | 초대 코드로 그룹 미리보기 | Public |
| POST | `/api/groups/{id}/join` | 그룹 참여 (sign-in 또는 guest, body에 displayName) | Public |

### Expenses
| Method | Endpoint | 설명 | 권한 |
|---|---|---|---|
| POST | `/api/groups/{groupId}/expenses` | 지출 추가 | 그룹 멤버 (sign-in + guest) |
| GET | `/api/groups/{groupId}/expenses` | 지출 목록 조회 | 그룹 멤버 |
| PUT | `/api/expenses/{id}` | 지출 수정 | 작성자 본인 + sign-in 유저만 |
| DELETE | `/api/expenses/{id}` | 지출 삭제 | 작성자 본인 + sign-in 유저만 |

### Balances / Settlement
| Method | Endpoint | 설명 | 권한 |
|---|---|---|---|
| GET | `/api/groups/{groupId}/balances` | 현재 잔액 상태 (누가 누구에게 얼마) | 그룹 멤버 |
| POST | `/api/groups/{groupId}/settle` | 정산 실행 (스냅샷 저장) + 메시지 생성용 데이터 반환 | 그룹 멤버 |

### AI Chat
| Method | Endpoint | 설명 | 권한 |
|---|---|---|---|
| POST | `/api/groups/{groupId}/ai-chat` | 자연어 입력 → 지출 데이터로 변환 (확인 후 저장은 별도 단계) | Sign-in만 |

---

## 6. 정산 알고리즘 (최소 송금 횟수)

### 목표
그룹 내 여러 명이 여러 건의 지출을 나눴을 때, 실제로 주고받아야 할 **송금 횟수를 최소화**하는 것.

### 로직 (Greedy 방식)

```
1. 각 멤버의 순잔액(net balance) 계산
   net_balance[member] = (그 사람이 낸 총액) - (그 사람이 부담해야 할 총액)
   → 양수면 "받을 돈이 있음(채권자)", 음수면 "줄 돈이 있음(채무자)"

2. 채권자 리스트(양수)와 채무자 리스트(음수)를 각각 만들고 절댓값 기준 내림차순 정렬

3. 반복:
   a. 가장 금액 큰 채권자(creditor)와 가장 금액 큰 채무자(debtor)를 뽑음
   b. 두 금액 중 작은 쪽(min(|creditor_amount|, |debtor_amount|))만큼 송금 기록 생성
      "{debtor}가 {creditor}에게 {amount} 송금"
   c. 두 사람의 잔액에서 해당 금액을 차감
   d. 잔액이 0이 된 사람을 리스트에서 제거
   e. 채권자/채무자 리스트가 모두 빌 때까지 반복

4. 결과: 송금 기록 리스트 (최소 송금 건수 보장)
```

### 의사코드 (C#)

```csharp
public List<Transaction> CalculateMinimumSettlement(Dictionary<Guid, decimal> netBalances)
{
    var creditors = netBalances.Where(x => x.Value > 0)
                                .OrderByDescending(x => x.Value)
                                .ToList();
    var debtors = netBalances.Where(x => x.Value < 0)
                              .OrderBy(x => x.Value) // 가장 큰 음수부터
                              .ToList();

    var transactions = new List<Transaction>();
    int ci = 0, di = 0;

    while (ci < creditors.Count && di < debtors.Count)
    {
        var creditor = creditors[ci];
        var debtor = debtors[di];

        decimal amount = Math.Min(creditor.Value, -debtor.Value);

        transactions.Add(new Transaction
        {
            FromMemberId = debtor.Key,
            ToMemberId = creditor.Key,
            Amount = amount
        });

        creditors[ci] = new(creditor.Key, creditor.Value - amount);
        debtors[di] = new(debtor.Key, debtor.Value + amount);

        if (creditors[ci].Value == 0) ci++;
        if (debtors[di].Value == 0) di++;
    }

    return transactions;
}
```

**README에 쓸 포인트**: 이 그리디 알고리즘이 항상 이론적 최적 송금 횟수(n-1건, n=채무자+채권자 수)를 보장하지 않지만 실용적으로 매우 근접한 결과를 빠르게(O(n log n)) 만들어냄. 완전 최적해를 구하려면 NP-hard에 가까운 조합 최적화가 필요하다는 점도 언급하면 알고리즘 이해도를 보여줄 수 있음.

---

## 7. AI 챗 설계 (자연어 → 구조화된 지출 데이터)

### 방식: Function Calling / Tool Use (OpenAI API)

AI 모델(OpenAI GPT)에게 자유 텍스트 응답이 아니라 **구조화된 도구 호출**을 하도록 강제.

### Tool 정의 예시

```json
{
  "name": "log_expense",
  "description": "사용자의 자연어 설명을 바탕으로 그룹 지출 항목을 구조화된 형태로 기록한다",
  "input_schema": {
    "type": "object",
    "properties": {
      "description": { "type": "string", "description": "지출 내용 (예: 저녁식사, 택시비)" },
      "totalAmount": { "type": "number" },
      "paidBy": { "type": "string", "description": "지출한 사람의 이름" },
      "shares": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "memberName": { "type": "string" },
            "amount": { "type": "number" }
          },
          "required": ["memberName", "amount"]
        }
      },
      "needsClarification": { "type": "boolean" },
      "clarificationQuestion": { "type": "string", "description": "정보가 부족할 때 되물을 질문" }
    },
    "required": ["description", "totalAmount", "paidBy", "shares", "needsClarification"]
  }
}
```

### 시스템 프롬프트 개요

```
너는 그룹 지출 정산 앱의 AI 비서다.
사용자의 자연어 입력을 분석해서 log_expense 도구를 호출해라.

규칙:
- 그룹 멤버 목록: {member_names}
- 금액이나 인원이 불명확하면 needsClarification=true로 하고 무엇이 부족한지 명시해라.
- 균등 분배가 기본이지만, 사용자가 "나만 뺴/더" 같은 조정을 언급하면 반영해라.
- 화폐 단위는 별도 언급 없으면 그룹 기본 통화로 간주해라.
```

### 흐름
1. 사용자가 자연어 입력 ("오늘 저녁 90불 썼는데 나 빼고 셋이서 20불 덜")
2. 백엔드가 그룹 멤버 목록을 컨텍스트로 포함해서 AI 호출
3. AI가 `log_expense` 도구 호출 형태로 구조화된 JSON 반환
4. `needsClarification: true`면 되묻는 질문을 챗 UI에 표시, 아니면
5. 프론트엔드에서 파싱된 결과를 **확인 카드**로 보여줌 ("이렇게 기록할까요? [확인] [수정]")
6. 사용자가 확인하면 `/api/groups/{groupId}/expenses`로 실제 저장

> 중요: AI가 바로 DB에 쓰지 않고, 반드시 사용자 확인 단계를 거치게 설계 → 이건 "AI를 신뢰하되 검증한다"는 책임감 있는 통합 사례로 README/인터뷰에서 어필하기 좋음.

---

## 8. 초대 플로우

1. Sign-in 유저가 그룹 생성 → `InviteCode` 자동 생성 (예: 8자리 랜덤 문자열)
2. 초대 링크 형태: `https://[도메인]/join/{inviteCode}`
3. 링크 공유 (카톡, 문자 등 앱 외부 채널)
4. 받은 사람이 링크 클릭 → 그룹 이름/멤버 미리보기 (`GET /api/groups/join/{inviteCode}`)
5. "참여하기" 클릭 → 로그인 상태면 sign-in 멤버로, 아니면 이름만 입력하고 guest 멤버로 참여 (`POST /api/groups/{id}/join`)

---

## 9. 정산 메시지 생성 & 공유

정산 계산 완료 후, 각 송금 건에 대해 아래 형태의 메시지를 생성:

```
[그룹 이름] 정산 안내

{받을사람}님, {보낼사람}님에게 {금액} 받으시면 됩니다.
(또는 반대 방향이면: {보낼사람}님, {받을사람}님에게 {금액} 보내주세요)

계좌: {은행명} {계좌번호} ({예금주명})

정산 내역 전체 보기: {공유링크}
```

### 계좌번호 소스
- Sign-in 유저(받는 사람)가 프로필에 계좌 등록해뒀으면 자동으로 복호화해서 메시지에 삽입
- 등록 안 했거나 guest면, 메시지 생성 화면에서 그 자리에서 입력받아 삽입 (저장 안 함)

### 공유 채널
| 채널 | 구현 방식 |
|---|---|
| 이메일 | `mailto:?subject=...&body=...` 링크 |
| WhatsApp | `https://wa.me/?text=...` 링크 |
| Discord | 별도 URL scheme 없음 → "메시지 복사" 버튼으로 클립보드 복사 후 사용자가 직접 붙여넣기 |

---

## 10. 페이지 / 라우트 구조 (Frontend)

```
/                       - 랜딩 페이지 (로그인/회원가입 유도)
/login                  - 로그인
/register               - 회원가입
/dashboard              - 내 그룹 목록 (sign-in 유저)
/groups/new             - 그룹 생성
/groups/:id             - 그룹 상세 (멤버, 지출 목록, 현재 잔액)
/groups/:id/expenses/new - 지출 추가 폼
/groups/:id/chat        - AI 챗 인터페이스 (sign-in만 접근 가능)
/groups/:id/settle      - 정산 메시지 생성/공유 화면
/join/:inviteCode       - 초대 링크 진입 페이지 (참여 방식 선택)
/profile                - 내 프로필 (계좌번호 등록/수정)
```

---

## 11. 보안 고려사항 (Advanced Requirement로 README에 쓸 항목)

1. **비밀번호 해싱**: bcrypt 또는 ASP.NET Core Identity 기본 해싱(PBKDF2) 사용
2. **계좌번호 암호화**: AES-256 등 양방향 암호화. 해싱과의 차이(복호화 필요 여부)를 README에 명시
3. **Authorization (RBAC 성격)**: guest는 편집/삭제/AI챗 API 접근 불가 → 미들웨어 또는 서비스 레이어에서 멤버의 `IsGuest` 여부로 체크
4. **Data validation**: 지출 금액이 음수/0인지, ExpenseShares 합계가 TotalAmount와 일치하는지 서버 사이드 검증
5. **초대 코드**: 추측 불가능한 충분한 길이의 랜덤 문자열 (예: nanoid 8~10자)

---

## 12. 개발 순서 제안

1. DB 스키마 + EF Core 마이그레이션 (SQLite provider로 시작)
2. Auth (회원가입/로그인/JWT)
3. Groups + 초대 플로우 + Guest 참여
4. Expenses CRUD (+ 권한 체크: guest는 수정/삭제 불가)
5. 잔액 계산 + 최소 송금 알고리즘 (유닛테스트 여기 집중)
6. 정산 메시지 생성 + 공유 링크
7. AI 챗 (OpenAI API function calling 연동)
8. Frontend 전체 UI 붙이기 (Tailwind + shadcn/ui) + 반응형
9. 유닛테스트 (BE: 정산 알고리즘 / 권한 로직 위주, xUnit)
10. 데모용 시드 데이터 스크립트 작성 (recruiter가 회원가입 없이 바로 둘러볼 수 있는 샘플 그룹/지출)
11. GitHub Actions CI (build + test on push) 구성
12. 배포: Frontend → Vercel / Backend → Render 또는 Railway / DB → Neon Postgres
13. README 작성 (스크린샷/데모 GIF, 라이브 데모 링크, 아키텍처 설명), `/specs` 폴더에 AI 프롬프트 기록
14. (시간 남으면) 영수증 OCR 추가

---

## 13. 나중에 추가 (2단계, OCR)

- 영수증 이미지 업로드 → GPT-4 Vision 등으로 항목/금액 자동 추출
- 추출 결과를 지출 입력 폼에 프리필, 사용자가 확인/수정 후 저장
- 지금 스코프에는 포함하지 않음 → 1단계 완성 후 별도로 진행
