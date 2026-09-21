import { computed, defineComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { useExecutionStore } from '@/stores/execution'

/**
 * Statuses whose tag carries a verdict. Anything else is reported as neutral instead of being coloured by
 * guesswork: the vocabulary of the server is wider than the four outcomes the operator acts on.
 */
const statusTagTypes: Partial<Record<server.ExecutionStatus, 'success' | 'warning' | 'danger'>> = {
  CompletedNominal: 'success',
  CompletedPartial: 'warning',
  CompensationRequired: 'danger',
  ReconciliationRequired: 'danger',
  Blocked: 'danger',
  Failed: 'danger',
}

const legTagTypes: Partial<Record<server.ExecutionLegStatus, 'success' | 'warning' | 'danger'>> = {
  Filled: 'success',
  PartiallyFilled: 'warning',
  Rejected: 'danger',
  TimedOut: 'danger',
  ReconciliationRequired: 'danger',
}

export default defineComponent({
  name: 'ExecutionView',
  setup() {
    const { t } = useI18n()
    const executionStore = useExecutionStore()

    const isCompensationDialogOpen = ref(false)
    const compensationReason = ref('')

    const compensationDisabled = computed(() => compensationReason.value.trim().length === 0)

    function formatTimestamp(value: string | null): string {
      return value ? new Date(value).toLocaleString('it-IT') : t('risk.notAvailable')
    }

    function formatIdentifier(value: string | null): string {
      return value ? value.slice(0, 8) : t('risk.notAvailable')
    }

    function statusTag(status: server.ExecutionStatus): 'success' | 'warning' | 'danger' | 'info' {
      return statusTagTypes[status] ?? 'info'
    }

    function legTag(status: server.ExecutionLegStatus): 'success' | 'warning' | 'danger' | 'info' {
      return legTagTypes[status] ?? 'info'
    }

    /**
     * Element Plus renders cell templates once with an empty row, so a label is produced only when there is a
     * value to name: an absent value yields no text instead of a lookup for a key that cannot exist.
     */
    function statusLabel(status: server.ExecutionStatus | undefined): string {
      return status ? t(`executionStatus.${status}`) : ''
    }

    function policyLabel(policy: server.FailurePolicy | undefined): string {
      return policy ? t(`failurePolicy.${policy}`) : ''
    }

    function legStatusLabel(status: server.ExecutionLegStatus | undefined): string {
      return status ? t(`executionLegStatus.${status}`) : ''
    }

    function directionLabel(direction: server.LegDirection | undefined): string {
      return direction ? t(`legDirection.${direction}`) : ''
    }

    function eventKindLabel(kind: server.ExecutionEventKind | undefined): string {
      return kind ? t(`executionEventKind.${kind}`) : ''
    }

    function openCompensation(): void {
      compensationReason.value = ''
      isCompensationDialogOpen.value = true
    }

    function closeCompensation(): void {
      isCompensationDialogOpen.value = false
      compensationReason.value = ''
    }

    async function refresh(): Promise<void> {
      await executionStore.load()
    }

    async function openDetail(executionId: string): Promise<void> {
      await executionStore.selectExecution(executionId)
    }

    async function confirmCompensation(): Promise<void> {
      const executionId = executionStore.detail?.executionId

      if (!executionId) {
        return
      }

      const outcome = await executionStore.compensate(executionId, compensationReason.value.trim())

      closeCompensation()
      reportOutcome(outcome, 'execution.compensateDone')
    }

    function reportOutcome(outcome: string, successKey: string): void {
      if (outcome === 'Applied') {
        ElMessage.success(t(successKey))

        return
      }

      if (outcome === 'Refused') {
        // A refusal is not a failure: the server understood the command and said no, and it said why. The
        // reason is shown next to it instead of being replaced by a generic message.
        const reason = executionStore.lastRefusalReason

        ElMessage.warning(
          reason
            ? `${t('execution.compensateRefused')} (${reason})`
            : t('execution.compensateRefused'),
        )

        return
      }

      if (outcome === 'NotFound') {
        ElMessage.error(t('execution.outcome.NotFound'))

        return
      }

      ElMessage.error(t('execution.compensateFailed'))
    }

    onMounted(refresh)

    return {
      t,
      executionStore,
      isCompensationDialogOpen,
      compensationReason,
      compensationDisabled,
      formatTimestamp,
      formatIdentifier,
      statusTag,
      legTag,
      statusLabel,
      policyLabel,
      legStatusLabel,
      directionLabel,
      eventKindLabel,
      refresh,
      openDetail,
      openCompensation,
      closeCompensation,
      confirmCompensation,
    }
  },
})
