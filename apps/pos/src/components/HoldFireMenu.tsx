import { useState } from 'react'
import { useOrder } from '../contexts/OrderContext'

interface HoldFireMenuProps {
  onClose: () => void
}

const COURSE_NAMES: Record<number, string> = {
  1: 'Appetizers',
  2: 'Mains',
  3: 'Desserts',
  4: 'Drinks',
}

export default function HoldFireMenu({ onClose }: HoldFireMenuProps) {
  const {
    order,
    selectedLineIds,
    holdItemsAsync,
    releaseItemsAsync,
    setItemCourseAsync,
    fireItemsAsync,
    fireCourseAsync,
    fireAllAsync,
    exitEditMode,
  } = useOrder()
  const [showCourseSelect, setShowCourseSelect] = useState(false)
  const [holdReason, setHoldReason] = useState('')
  const [showHoldReason, setShowHoldReason] = useState(false)

  if (!order) {
    onClose()
    return null
  }

  const pendingLines = order.lines.filter((line) => !line.sentAt)
  const heldLines = order.lines.filter((line) => line.isHeld && !line.sentAt)
  const selectedPendingLines = pendingLines.filter((line) => selectedLineIds.includes(line.id))
  const selectedHeldLines = selectedPendingLines.filter((line) => line.isHeld)

  // Get unique course numbers with pending items
  const coursesWithPendingItems = [...new Set(
    pendingLines.map((line) => line.courseNumber ?? 1)
  )].sort((a, b) => a - b)

  async function handleHoldSelected() {
    if (selectedLineIds.length > 0) {
      await holdItemsAsync(selectedLineIds, holdReason || undefined)
      setHoldReason('')
      setShowHoldReason(false)
      exitEditMode()
      onClose()
    }
  }

  async function handleReleaseSelected() {
    const heldSelectedIds = selectedHeldLines.map((l) => l.id)
    if (heldSelectedIds.length > 0) {
      await releaseItemsAsync(heldSelectedIds)
      exitEditMode()
      onClose()
    }
  }

  async function handleSetCourse(courseNumber: number) {
    if (selectedLineIds.length > 0) {
      await setItemCourseAsync(selectedLineIds, courseNumber)
      setShowCourseSelect(false)
      exitEditMode()
      onClose()
    }
  }

  async function handleFireSelected() {
    const pendingSelectedIds = selectedPendingLines.map((l) => l.id)
    if (pendingSelectedIds.length > 0) {
      await fireItemsAsync(pendingSelectedIds)
      exitEditMode()
      onClose()
    }
  }

  async function handleFireCourse(courseNumber: number) {
    await fireCourseAsync(courseNumber)
    onClose()
  }

  async function handleFireAll() {
    if (pendingLines.length > 0 && confirm(`Fire all ${pendingLines.length} pending items to kitchen?`)) {
      await fireAllAsync()
      onClose()
    }
  }

  if (showHoldReason) {
    return (
      <dialog open aria-modal="true" className="actions-menu">
        <article>
          <header>
            <h3>Hold Items</h3>
          </header>

          <p>
            Hold {selectedPendingLines.length} selected item{selectedPendingLines.length !== 1 ? 's' : ''}
          </p>

          <label>
            Reason (optional)
            <input
              type="text"
              value={holdReason}
              onChange={(e) => setHoldReason(e.target.value)}
              placeholder="e.g. Wait for guest, Course timing"
            />
          </label>

          <footer>
            <button type="button" className="secondary" onClick={() => setShowHoldReason(false)}>
              Back
            </button>
            <button type="button" onClick={handleHoldSelected}>
              Hold Items
            </button>
          </footer>
        </article>
      </dialog>
    )
  }

  if (showCourseSelect) {
    return (
      <dialog open aria-modal="true" className="actions-menu">
        <article>
          <header>
            <h3>Set Course</h3>
          </header>

          <p>
            Assign {selectedLineIds.length} selected item{selectedLineIds.length !== 1 ? 's' : ''} to course:
          </p>

          <nav>
            <ul role="list" className="actions-list">
              {[1, 2, 3, 4].map((course) => (
                <li key={course}>
                  <button type="button" onClick={() => handleSetCourse(course)}>
                    Course {course}: {COURSE_NAMES[course]}
                  </button>
                </li>
              ))}
            </ul>
          </nav>

          <footer>
            <button type="button" className="secondary" onClick={() => setShowCourseSelect(false)}>
              Back
            </button>
          </footer>
        </article>
      </dialog>
    )
  }

  return (
    <dialog open aria-modal="true" className="actions-menu">
      <article>
        <header>
          <h3>Hold / Fire</h3>
        </header>

        <nav>
          <ul role="list" className="actions-list">
            {/* Selected items actions */}
            {selectedLineIds.length > 0 && (
              <>
                <li>
                  <strong>Selected Items ({selectedLineIds.length})</strong>
                </li>
                <li>
                  <button
                    type="button"
                    onClick={() => setShowHoldReason(true)}
                    disabled={selectedPendingLines.length === 0}
                  >
                    Hold Selected
                  </button>
                </li>
                <li>
                  <button
                    type="button"
                    onClick={handleReleaseSelected}
                    disabled={selectedHeldLines.length === 0}
                  >
                    Release Selected ({selectedHeldLines.length} held)
                  </button>
                </li>
                <li>
                  <button
                    type="button"
                    onClick={() => setShowCourseSelect(true)}
                    disabled={selectedLineIds.length === 0}
                  >
                    Set Course
                  </button>
                </li>
                <li>
                  <button
                    type="button"
                    className="contrast"
                    onClick={handleFireSelected}
                    disabled={selectedPendingLines.length === 0}
                  >
                    Fire Selected to Kitchen
                  </button>
                </li>
                <li><hr /></li>
              </>
            )}

            {/* Fire by course */}
            {coursesWithPendingItems.length > 0 && (
              <>
                <li>
                  <strong>Fire by Course</strong>
                </li>
                {coursesWithPendingItems.map((course) => {
                  const courseItems = pendingLines.filter((l) => (l.courseNumber ?? 1) === course)
                  return (
                    <li key={course}>
                      <button type="button" onClick={() => handleFireCourse(course)}>
                        Fire Course {course}: {COURSE_NAMES[course]} ({courseItems.length} items)
                      </button>
                    </li>
                  )
                })}
                <li><hr /></li>
              </>
            )}

            {/* Global actions */}
            <li>
              <strong>All Items</strong>
            </li>
            <li>
              <button
                type="button"
                className="contrast"
                onClick={handleFireAll}
                disabled={pendingLines.length === 0}
              >
                Fire All ({pendingLines.length} pending)
              </button>
            </li>

            {/* Summary */}
            {heldLines.length > 0 && (
              <li>
                <small style={{ color: 'var(--pico-color-orange-550)' }}>
                  {heldLines.length} item{heldLines.length !== 1 ? 's' : ''} on hold
                </small>
              </li>
            )}
          </ul>
        </nav>

        <footer>
          <button type="button" className="secondary" onClick={onClose}>
            Close
          </button>
        </footer>
      </article>
    </dialog>
  )
}
