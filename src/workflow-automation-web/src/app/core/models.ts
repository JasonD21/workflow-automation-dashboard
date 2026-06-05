export interface AuthResponse {
  accessToken: string;
  expiresAt?: string;
}

export interface UserDto {
  id: string;
  email: string;
  displayName?: string;
  timeZone?: string;
  isDemo: boolean;
}

export interface ConnectionBrief {
  id: string;
  provider: string;
  displayName: string;
  status: string;
}

export interface RunBrief {
  id: string;
  automationId: string;
  automationName: string;
  status: string;
  triggeredAt: string;
  isTest: boolean;
}

export interface ReportBrief {
  id: string;
  name: string;
  dayOfWeek: string;
  timeOfDay: string;
}

export interface DashboardSummary {
  connections: ConnectionBrief[];
  enabledAutomations: number;
  runsLast7Days: number;
  failedRunsLast7Days: number;
  recentRuns: RunBrief[];
  nextReport: ReportBrief | null;
}

export interface Connection {
  id: string;
  provider: string;
  displayName: string;
  status: string;
  createdAt?: string;
  lastRefreshedAt?: string;
}

export interface AuthorizeUrl {
  url: string;
}

export interface CatalogField {
  key: string;
  label: string;
  type: string;
  required: boolean;
}

export interface TriggerDefinition {
  type: string;
  provider: string;
  displayName: string;
  description: string;
  configFields: CatalogField[];
  tokens: string[];
}

export interface ActionDefinition {
  type: string;
  provider: string;
  displayName: string;
  description: string;
  requiresConnection: boolean;
  configFields: CatalogField[];
  templatedFields: string[];
}

export interface FilterCondition {
  field: string;
  operator: string;
  value: string;
}

export interface Automation {
  id: string;
  name: string;
  isEnabled: boolean;
  triggerType: string;
  triggerProvider: string;
  triggerConnectionId: string;
  triggerConfig?: Record<string, string> | null;
  filter?: FilterCondition | null;
  actionType: string;
  actionProvider: string;
  actionConnectionId?: string | null;
  actionConfig: Record<string, string>;
  lastTriggeredAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SaveAutomation {
  name: string;
  triggerType: string;
  triggerConnectionId: string;
  triggerConfig?: Record<string, string> | null;
  filter?: FilterCondition | null;
  actionType: string;
  actionConnectionId?: string | null;
  actionConfig?: Record<string, string> | null;
}

export interface Run {
  id: string;
  automationId: string;
  isTest: boolean;
  triggeredAt: string;
  status: string;
  idempotencyKey?: string | null;
  triggerPayloadSummary?: string | null;
  actionResultSummary?: string | null;
  errorMessage?: string | null;
  durationMs?: number | null;
}

export interface Paged<T> {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
}

export interface ReportSchedule {
  id: string;
  name: string;
  isEnabled: boolean;
  dayOfWeek: string;
  timeOfDay: string;
  timeZone: string;
  includedSources: string[];
  recipientEmail: string;
  lastRunAt?: string | null;
}

export interface SaveReportSchedule {
  name: string;
  isEnabled: boolean;
  dayOfWeek: string;
  timeOfDay: string;
  timeZone: string;
  includedSources: string[];
  recipientEmail: string | null;
}

export interface GeneratedReportSummary {
  id: string;
  reportScheduleId: string;
  generatedAt: string;
  periodStart: string;
  periodEnd: string;
  emailStatus: string;
  emailedAt?: string | null;
}

export interface GeneratedReportDetail extends GeneratedReportSummary {
  dataSnapshot: string;
  renderedHtml: string;
}
