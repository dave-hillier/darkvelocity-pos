import { useState, useCallback } from 'react'
import { apiClient } from '../api/client'

export interface MailboxConfig {
  configId: string
  displayName: string
  host: string
  port: number
  username: string
  useSsl: boolean
  folderName: string
  isEnabled: boolean
  defaultDocumentType: string
  lastPollAt: string | null
}

export interface RoutingRule {
  ruleId: string
  name: string
  type: 'SenderDomain' | 'SenderEmail' | 'SubjectPattern'
  pattern: string
  suggestedDocumentType: string | null
  suggestedVendorId: string | null
  suggestedVendorName: string | null
  autoApprove: boolean
  priority: number
}

export interface AgentSnapshot {
  organizationId: string
  siteId: string
  isActive: boolean
  mailboxCount: number
  pollingIntervalMinutes: number
  autoProcessEnabled: boolean
  autoProcessConfidenceThreshold: number
  pendingItemCount: number
  lastPollAt: string | null
  totalPolls: number
  totalEmailsFetched: number
  totalDocumentsCreated: number
  totalAutoProcessed: number
  mailboxes: MailboxConfig[]
  routingRules: RoutingRule[]
  _links: Record<string, { href: string }>
}

export interface QueueItem {
  planId: string
  emailFrom: string
  emailSubject: string
  emailReceivedAt: string
  attachmentCount: number
  suggestedDocumentType: string
  suggestedVendorName: string | null
  typeConfidence: number
  vendorConfidence: number
  suggestedAction: 'AutoProcess' | 'ManualReview'
  reasoning: string
  proposedAt: string
}

export interface PlanSnapshot {
  planId: string
  status: string
  emailFrom: string
  emailSubject: string
  emailReceivedAt: string
  attachmentCount: number
  suggestedDocumentType: string
  suggestedVendorName: string | null
  typeConfidence: number
  vendorConfidence: number
  suggestedAction: string
  reasoning: string
  overrideDocumentType: string | null
  overrideVendorName: string | null
  documentIds: string[]
  reviewedBy: string | null
  reviewedAt: string | null
  rejectionReason: string | null
  proposedAt: string
}

export interface HistoryEntry {
  entryId: string
  emailMessageId: string
  from: string
  subject: string
  receivedAt: string
  processedAt: string
  outcome: 'PendingReview' | 'AutoProcessed' | 'Duplicate' | 'Rejected' | 'Failed'
  planId: string | null
  documentIds: string[] | null
  error: string | null
}

export interface PollResult {
  emailsFetched: number
  newPendingItems: number
  autoProcessedItems: number
  duplicatesSkipped: number
  errors: number
}

export function useIngestionAgent() {
  const [agent, setAgent] = useState<AgentSnapshot | null>(null)
  const [queue, setQueue] = useState<QueueItem[]>([])
  const [history, setHistory] = useState<HistoryEntry[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const basePath = useCallback(() => {
    return apiClient.buildOrgSitePath('/ingestion-agent')
  }, [])

  const loadAgent = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const data = await apiClient.get<AgentSnapshot>(basePath())
      setAgent(data)
    } catch (err) {
      if ((err as Error).message.includes('404')) {
        setAgent(null)
      } else {
        setError((err as Error).message)
      }
    } finally {
      setIsLoading(false)
    }
  }, [basePath])

  const configureAgent = useCallback(async (config: {
    pollingIntervalMinutes?: number
    autoProcessEnabled?: boolean
    autoProcessConfidenceThreshold?: number
  }) => {
    setError(null)
    try {
      const data = await apiClient.post<AgentSnapshot>(basePath(), config)
      setAgent(data)
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath])

  const updateSettings = useCallback(async (settings: {
    pollingIntervalMinutes?: number
    autoProcessEnabled?: boolean
    slackWebhookUrl?: string
    slackNotifyOnNewItem?: boolean
  }) => {
    setError(null)
    try {
      const data = await apiClient.patch<AgentSnapshot>(basePath(), settings)
      setAgent(data)
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath])

  const activate = useCallback(async () => {
    setError(null)
    try {
      const data = await apiClient.post<AgentSnapshot>(`${basePath()}/activate`)
      setAgent(data)
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath])

  const deactivate = useCallback(async () => {
    setError(null)
    try {
      const data = await apiClient.post<AgentSnapshot>(`${basePath()}/deactivate`)
      setAgent(data)
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath])

  const triggerPoll = useCallback(async () => {
    setError(null)
    try {
      const result = await apiClient.post<PollResult>(`${basePath()}/poll`)
      await loadAgent()
      return result
    } catch (err) {
      setError((err as Error).message)
      return null
    }
  }, [basePath, loadAgent])

  const loadQueue = useCallback(async () => {
    setError(null)
    try {
      const data = await apiClient.get<{ _embedded: { items: QueueItem[] } }>(`${basePath()}/queue`)
      setQueue(data._embedded?.items ?? [])
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath])

  const loadHistory = useCallback(async (limit = 50) => {
    setError(null)
    try {
      const data = await apiClient.get<{ _embedded: { items: HistoryEntry[] } }>(`${basePath()}/history?limit=${limit}`)
      setHistory(data._embedded?.items ?? [])
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath])

  const approvePlan = useCallback(async (planId: string, approvedBy: string) => {
    setError(null)
    try {
      await apiClient.post(`${basePath()}/plans/${planId}/approve`, { approvedBy })
      await loadQueue()
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath, loadQueue])

  const modifyPlan = useCallback(async (planId: string, modification: {
    modifiedBy: string
    documentType?: string
    vendorId?: string
    vendorName?: string
  }) => {
    setError(null)
    try {
      await apiClient.post(`${basePath()}/plans/${planId}/modify`, modification)
      await loadQueue()
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath, loadQueue])

  const rejectPlan = useCallback(async (planId: string, rejectedBy: string, reason?: string) => {
    setError(null)
    try {
      await apiClient.post(`${basePath()}/plans/${planId}/reject`, { rejectedBy, reason })
      await loadQueue()
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath, loadQueue])

  const addMailbox = useCallback(async (mailbox: {
    displayName: string
    host: string
    port: number
    username: string
    password: string
    useSsl?: boolean
    folderName?: string
  }) => {
    setError(null)
    try {
      const data = await apiClient.post<AgentSnapshot>(`${basePath()}/mailboxes`, mailbox)
      setAgent(data)
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath])

  const removeMailbox = useCallback(async (configId: string) => {
    setError(null)
    try {
      const data = await apiClient.delete(`${basePath()}/mailboxes/${configId}`) as unknown as AgentSnapshot
      setAgent(data)
      await loadAgent()
    } catch (err) {
      setError((err as Error).message)
    }
  }, [basePath, loadAgent])

  return {
    agent,
    queue,
    history,
    isLoading,
    error,
    loadAgent,
    configureAgent,
    updateSettings,
    activate,
    deactivate,
    triggerPoll,
    loadQueue,
    loadHistory,
    approvePlan,
    modifyPlan,
    rejectPlan,
    addMailbox,
    removeMailbox,
  }
}
