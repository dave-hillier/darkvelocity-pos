import { useEffect, useState } from 'react'
import { useIngestionAgent, type QueueItem } from '../hooks/useIngestionAgent'

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function formatConfidence(value: number): string {
  return `${Math.round(value * 100)}%`
}

function getOutcomeBadgeClass(outcome: string): string {
  switch (outcome) {
    case 'PendingReview': return 'badge-warning'
    case 'AutoProcessed': return 'badge-success'
    case 'Duplicate': return ''
    case 'Rejected': return 'badge-danger'
    case 'Failed': return 'badge-danger'
    default: return ''
  }
}

export default function IngestionQueuePage() {
  const {
    agent,
    queue,
    history,
    isLoading,
    error,
    loadAgent,
    configureAgent,
    loadQueue,
    loadHistory,
    triggerPoll,
    approvePlan,
    rejectPlan,
    activate,
    deactivate,
  } = useIngestionAgent()

  const [tab, setTab] = useState<'queue' | 'history'>('queue')
  const [polling, setPolling] = useState(false)
  const [rejectingId, setRejectingId] = useState<string | null>(null)
  const [rejectReason, setRejectReason] = useState('')

  useEffect(() => {
    loadAgent()
  }, [loadAgent])

  useEffect(() => {
    if (agent) {
      if (tab === 'queue') loadQueue()
      else loadHistory()
    }
  }, [agent, tab, loadQueue, loadHistory])

  const handlePoll = async () => {
    setPolling(true)
    await triggerPoll()
    await loadQueue()
    setPolling(false)
  }

  const handleApprove = async (item: QueueItem) => {
    await approvePlan(item.planId, '00000000-0000-0000-0000-000000000000') // TODO: current user
    await loadQueue()
  }

  const handleReject = async (planId: string) => {
    await rejectPlan(planId, '00000000-0000-0000-0000-000000000000', rejectReason)
    setRejectingId(null)
    setRejectReason('')
    await loadQueue()
  }

  if (isLoading) {
    return (
      <>
        <hgroup>
          <h1>Document Inbox</h1>
          <p>Review incoming invoices and receipts</p>
        </hgroup>
        <article aria-busy="true">Loading...</article>
      </>
    )
  }

  // Agent not configured — show setup
  if (!agent) {
    return (
      <>
        <hgroup>
          <h1>Document Inbox</h1>
          <p>Review incoming invoices and receipts</p>
        </hgroup>
        <article>
          <header>
            <strong>Ingestion Agent Not Configured</strong>
          </header>
          <p>Set up the invoice ingestion agent to start polling your email for invoices and receipts.</p>
          <button onClick={() => configureAgent({})}>
            Configure Agent
          </button>
        </article>
      </>
    )
  }

  return (
    <>
      <hgroup>
        <h1>Document Inbox</h1>
        <p>Review incoming invoices and receipts</p>
      </hgroup>

      {error && (
        <article style={{ background: 'var(--pico-del-color)', padding: '1rem', marginBottom: '1rem' }}>
          <p>{error}</p>
        </article>
      )}

      {/* Agent status bar */}
      <article style={{ marginBottom: '1rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
          <div>
            <strong>Agent Status: </strong>
            <span className={agent.isActive ? 'badge-success' : 'badge-warning'}>
              {agent.isActive ? 'Active' : 'Paused'}
            </span>
            {agent.lastPollAt && (
              <small style={{ marginLeft: '1rem' }}>
                Last poll: {formatDate(agent.lastPollAt)}
              </small>
            )}
          </div>
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            {agent.isActive ? (
              <button className="outline" onClick={deactivate}>Pause</button>
            ) : (
              <button onClick={activate}>Activate</button>
            )}
            <button
              className="secondary"
              onClick={handlePoll}
              aria-busy={polling}
              disabled={polling}
            >
              Poll Now
            </button>
          </div>
        </div>
        <div style={{ display: 'flex', gap: '2rem', marginTop: '0.75rem', fontSize: '0.875rem' }}>
          <span>Mailboxes: {agent.mailboxCount}</span>
          <span>Fetched: {agent.totalEmailsFetched}</span>
          <span>Auto-processed: {agent.totalAutoProcessed}</span>
          <span>Pending: {agent.pendingItemCount}</span>
        </div>
      </article>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem' }}>
        <button
          className={tab === 'queue' ? '' : 'outline'}
          onClick={() => setTab('queue')}
        >
          Queue ({queue.length})
        </button>
        <button
          className={tab === 'history' ? '' : 'outline'}
          onClick={() => setTab('history')}
        >
          History
        </button>
      </div>

      {tab === 'queue' ? (
        queue.length === 0 ? (
          <article>
            <p>No pending items. All caught up.</p>
          </article>
        ) : (
          <table>
            <thead>
              <tr>
                <th>From</th>
                <th>Subject</th>
                <th>Received</th>
                <th>Type</th>
                <th>Vendor</th>
                <th>Confidence</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {queue.map((item) => (
                <tr key={item.planId}>
                  <td>{item.emailFrom}</td>
                  <td>{item.emailSubject}</td>
                  <td>{formatDate(item.emailReceivedAt)}</td>
                  <td>{item.suggestedDocumentType}</td>
                  <td>{item.suggestedVendorName ?? '—'}</td>
                  <td>{formatConfidence(item.typeConfidence)}</td>
                  <td>
                    <div style={{ display: 'flex', gap: '0.25rem' }}>
                      <button
                        className="outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.8rem' }}
                        onClick={() => handleApprove(item)}
                      >
                        Approve
                      </button>
                      {rejectingId === item.planId ? (
                        <div style={{ display: 'flex', gap: '0.25rem', alignItems: 'center' }}>
                          <input
                            type="text"
                            placeholder="Reason"
                            value={rejectReason}
                            onChange={(e) => setRejectReason(e.target.value)}
                            style={{ padding: '0.25rem', fontSize: '0.8rem', width: '120px' }}
                          />
                          <button
                            className="secondary"
                            style={{ padding: '0.25rem 0.5rem', fontSize: '0.8rem' }}
                            onClick={() => handleReject(item.planId)}
                          >
                            Confirm
                          </button>
                          <button
                            className="outline secondary"
                            style={{ padding: '0.25rem 0.5rem', fontSize: '0.8rem' }}
                            onClick={() => setRejectingId(null)}
                          >
                            Cancel
                          </button>
                        </div>
                      ) : (
                        <button
                          className="secondary"
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.8rem' }}
                          onClick={() => setRejectingId(item.planId)}
                        >
                          Reject
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )
      ) : (
        <table>
          <thead>
            <tr>
              <th>From</th>
              <th>Subject</th>
              <th>Received</th>
              <th>Processed</th>
              <th>Outcome</th>
            </tr>
          </thead>
          <tbody>
            {history.length === 0 ? (
              <tr>
                <td colSpan={5}>No processing history yet.</td>
              </tr>
            ) : (
              history.map((entry) => (
                <tr key={entry.entryId}>
                  <td>{entry.from}</td>
                  <td>{entry.subject}</td>
                  <td>{formatDate(entry.receivedAt)}</td>
                  <td>{formatDate(entry.processedAt)}</td>
                  <td>
                    <span className={getOutcomeBadgeClass(entry.outcome)}>
                      {entry.outcome}
                    </span>
                    {entry.error && (
                      <small style={{ display: 'block', color: 'var(--pico-del-color)' }}>
                        {entry.error}
                      </small>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      )}
    </>
  )
}
