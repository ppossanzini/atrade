import { defineComponent, type PropType } from 'vue'
import { useI18n } from 'vue-i18n'

type TagType = 'success' | 'warning' | 'danger' | 'info' | 'primary'

const gateTagTypes: Record<server.RiskGateVerdict, TagType> = {
  Allow: 'success',
  Review: 'warning',
  Block: 'danger',
}

const statusTagTypes: Record<server.ProposalStatus, TagType> = {
  NeedsReview: 'warning',
  AutoApproved: 'success',
  Blocked: 'danger',
  Approved: 'success',
  Rejected: 'danger',
  Suspended: 'info',
  Expired: 'info',
}

/**
 * The operator queue. It is presentational: decidability arrives with each row from the server, so the
 * component never decides by itself whether an action is permitted. Actions are emitted, not performed.
 */
export default defineComponent({
  name: 'ProposalQueue',
  props: {
    proposals: {
      type: Array as PropType<server.ProposalSummary[]>,
      required: true,
    },
    isLoading: {
      type: Boolean,
      default: false,
    },
    isSaving: {
      type: Boolean,
      default: false,
    },
  },
  emits: ['select', 'decide'],
  setup(props, { emit }) {
    const { t } = useI18n()

    function gateTagType(gate: server.RiskGateVerdict): TagType {
      return gateTagTypes[gate] ?? 'info'
    }

    function statusTagType(status: server.ProposalStatus): TagType {
      return statusTagTypes[status] ?? 'info'
    }

    function actionTagType(action: server.ProposalAction): TagType {
      return action === 'Entry' ? 'success' : 'warning'
    }

    function formatTime(value: string): string {
      return new Date(value).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })
    }

    function isPastDeadline(value: string): boolean {
      return new Date(value).getTime() <= Date.now()
    }

    function shortId(proposalId: string): string {
      return proposalId.slice(0, 8)
    }

    function requestSelect(proposalId: string): void {
      emit('select', proposalId)
    }

    function requestDecision(kind: 'approve' | 'reject' | 'suspend', proposalId: string): void {
      emit('decide', { kind, proposalId })
    }

    return {
      t,
      gateTagType,
      statusTagType,
      actionTagType,
      formatTime,
      isPastDeadline,
      shortId,
      requestSelect,
      requestDecision,
    }
  },
})
