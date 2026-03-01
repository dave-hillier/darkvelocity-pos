import { useEffect, useState } from 'react'
import { useIngestionAgent } from '../hooks/useIngestionAgent'

export default function IngestionSettingsPage() {
  const {
    agent,
    isLoading,
    error,
    loadAgent,
    updateSettings,
    addMailbox,
    removeMailbox,
  } = useIngestionAgent()

  const [showAddMailbox, setShowAddMailbox] = useState(false)
  const [mailboxForm, setMailboxForm] = useState({
    displayName: '',
    host: '',
    port: 993,
    username: '',
    password: '',
    useSsl: true,
    folderName: 'INBOX',
  })

  useEffect(() => {
    loadAgent()
  }, [loadAgent])

  const handleAddMailbox = async () => {
    await addMailbox(mailboxForm)
    setShowAddMailbox(false)
    setMailboxForm({
      displayName: '',
      host: '',
      port: 993,
      username: '',
      password: '',
      useSsl: true,
      folderName: 'INBOX',
    })
  }

  if (isLoading) {
    return (
      <>
        <hgroup>
          <h1>Ingestion Settings</h1>
          <p>Configure the invoice ingestion agent</p>
        </hgroup>
        <article aria-busy="true">Loading...</article>
      </>
    )
  }

  if (!agent) {
    return (
      <>
        <hgroup>
          <h1>Ingestion Settings</h1>
          <p>Configure the invoice ingestion agent</p>
        </hgroup>
        <article>
          <p>Agent not configured. Go to the Document Inbox page to set up.</p>
        </article>
      </>
    )
  }

  return (
    <>
      <hgroup>
        <h1>Ingestion Settings</h1>
        <p>Configure the invoice ingestion agent</p>
      </hgroup>

      {error && (
        <article style={{ background: 'var(--pico-del-color)', padding: '1rem', marginBottom: '1rem' }}>
          <p>{error}</p>
        </article>
      )}

      {/* General settings */}
      <article>
        <header><strong>General Settings</strong></header>
        <form onSubmit={(e) => { e.preventDefault() }}>
          <label>
            Polling Interval (minutes)
            <input
              type="number"
              value={agent.pollingIntervalMinutes}
              min={1}
              max={60}
              onChange={(e) => updateSettings({ pollingIntervalMinutes: parseInt(e.target.value) })}
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={agent.autoProcessEnabled}
              onChange={(e) => updateSettings({ autoProcessEnabled: e.target.checked })}
            />
            Auto-process high-confidence items
          </label>
        </form>
      </article>

      {/* Mailboxes */}
      <article>
        <header>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <strong>Mailboxes</strong>
            <button
              className="outline"
              style={{ padding: '0.25rem 0.75rem', fontSize: '0.875rem' }}
              onClick={() => setShowAddMailbox(!showAddMailbox)}
            >
              {showAddMailbox ? 'Cancel' : 'Add Mailbox'}
            </button>
          </div>
        </header>

        {showAddMailbox && (
          <form onSubmit={(e) => { e.preventDefault(); handleAddMailbox() }} style={{ marginBottom: '1rem' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
              <label>
                Display Name
                <input
                  type="text"
                  value={mailboxForm.displayName}
                  onChange={(e) => setMailboxForm({ ...mailboxForm, displayName: e.target.value })}
                  required
                />
              </label>
              <label>
                IMAP Host
                <input
                  type="text"
                  value={mailboxForm.host}
                  placeholder="imap.example.com"
                  onChange={(e) => setMailboxForm({ ...mailboxForm, host: e.target.value })}
                  required
                />
              </label>
              <label>
                Port
                <input
                  type="number"
                  value={mailboxForm.port}
                  onChange={(e) => setMailboxForm({ ...mailboxForm, port: parseInt(e.target.value) })}
                />
              </label>
              <label>
                Username
                <input
                  type="text"
                  value={mailboxForm.username}
                  onChange={(e) => setMailboxForm({ ...mailboxForm, username: e.target.value })}
                  required
                />
              </label>
              <label>
                Password
                <input
                  type="password"
                  value={mailboxForm.password}
                  onChange={(e) => setMailboxForm({ ...mailboxForm, password: e.target.value })}
                  required
                />
              </label>
              <label>
                Folder
                <input
                  type="text"
                  value={mailboxForm.folderName}
                  onChange={(e) => setMailboxForm({ ...mailboxForm, folderName: e.target.value })}
                />
              </label>
            </div>
            <label>
              <input
                type="checkbox"
                checked={mailboxForm.useSsl}
                onChange={(e) => setMailboxForm({ ...mailboxForm, useSsl: e.target.checked })}
              />
              Use SSL/TLS
            </label>
            <button type="submit">Add Mailbox</button>
          </form>
        )}

        {agent.mailboxes.length === 0 ? (
          <p>No mailboxes configured. Add one to start polling for invoices.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Host</th>
                <th>Username</th>
                <th>Folder</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {agent.mailboxes.map((mb) => (
                <tr key={mb.configId}>
                  <td>{mb.displayName}</td>
                  <td>{mb.host}:{mb.port}</td>
                  <td>{mb.username}</td>
                  <td>{mb.folderName}</td>
                  <td>
                    <span className={mb.isEnabled ? 'badge-success' : 'badge-warning'}>
                      {mb.isEnabled ? 'Enabled' : 'Disabled'}
                    </span>
                  </td>
                  <td>
                    <button
                      className="secondary outline"
                      style={{ padding: '0.25rem 0.5rem', fontSize: '0.8rem' }}
                      onClick={() => removeMailbox(mb.configId)}
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </article>

      {/* Stats */}
      <article>
        <header><strong>Statistics</strong></header>
        <table>
          <tbody>
            <tr><td>Total Polls</td><td>{agent.totalPolls}</td></tr>
            <tr><td>Emails Fetched</td><td>{agent.totalEmailsFetched}</td></tr>
            <tr><td>Documents Created</td><td>{agent.totalDocumentsCreated}</td></tr>
            <tr><td>Auto-processed</td><td>{agent.totalAutoProcessed}</td></tr>
            <tr><td>Pending Review</td><td>{agent.pendingItemCount}</td></tr>
          </tbody>
        </table>
      </article>
    </>
  )
}
