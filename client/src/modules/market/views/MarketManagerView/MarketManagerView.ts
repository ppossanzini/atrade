import { computed, defineComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import ProposalQueue from '@/modules/market/components/ProposalQueue/ProposalQueue.vue'
import RiskGatePanel from '@/modules/risk/components/RiskGatePanel/RiskGatePanel.vue'
import { useBasketsStore } from '@/stores/baskets'
import { useExecutionStore } from '@/stores/execution'
import { useMarketStore, type ProposalDecisionKind } from '@/stores/market'

type DecisionRequest = {
  kind: ProposalDecisionKind
  proposalId: string
}

export default defineComponent({
  name: 'MarketManagerView',
  components: { ProposalQueue, RiskGatePanel },
  setup() {
    const { t } = useI18n()
    const marketStore = useMarketStore()
    const basketsStore = useBasketsStore()
    const executionStore = useExecutionStore()

    const pendingDecision = ref<DecisionRequest | null>(null)
    const decisionReason = ref('')

    const modeOptions = computed(() => [
      { label: t('marketManagerMode.Manual'), value: 'Manual' },
      { label: t('marketManagerMode.Supervised'), value: 'Supervised' },
      { label: t('marketManagerMode.Automatic'), value: 'Automatic' },
    ])

    const currentMode = computed(() => marketStore.manager?.mode ?? 'Supervised')
    const isAnalysisRunning = computed(() => marketStore.manager?.isAnalysisRunning ?? false)
    const policyHintKey = computed(() => `market.policy.${currentMode.value}`)

    const activeBasketLabel = computed(() => {
      const active = basketsStore.activeBasket

      return active ? `${active.name} · v${active.activeVersionNumber}` : t('market.noActiveBasket')
    })

    const lastCycleKey = computed(() =>
      marketStore.manager?.lastCycleAtUtc
        ? new Date(marketStore.manager.lastCycleAtUtc).toLocaleTimeString('it-IT')
        : t('status.never'),
    )

    // The drawer reuses the risk gate panel, so the gates of a proposal and the gates of a basket are
    // rendered by the same component and cannot drift apart.
    const decisionForPanel = computed<server.RiskDecision | null>(() => {
      const detail = marketStore.detail

      if (!detail) {
        return null
      }

      return {
        verdict: detail.gate,
        evaluatedAtUtc: detail.proposedAtUtc,
        basketId: detail.basketId,
        basketVersionId: null,
        versionNumber: detail.versionNumber,
        snapshotCapturedAtUtc: detail.snapshotCapturedAtUtc,
        gates: detail.gates,
      }
    })

    const reasonIsRequired = computed(
      () => pendingDecision.value !== null && pendingDecision.value.kind !== 'approve',
    )

    const confirmDisabled = computed(
      () => reasonIsRequired.value && decisionReason.value.trim().length === 0,
    )

    /**
     * Only an approved proposal can be executed, and only once. The already-executed half is read from the
     * execution queue so the button does not invite a command the server would refuse; the server remains
     * the authority on both conditions.
     */
    const canStartExecution = computed(
      () =>
        marketStore.detail !== null &&
        marketStore.detail.status === 'Approved' &&
        !executionStore.isProposalExecuted(marketStore.detail.proposalId),
    )

    const decisionDialogTitle = computed(() =>
      pendingDecision.value
        ? t(`market.confirm.${pendingDecision.value.kind}`)
        : t('market.confirmTitle'),
    )

    const decisionDialogBody = computed(() =>
      pendingDecision.value ? t(`market.confirmBody.${pendingDecision.value.kind}`) : '',
    )

    function formatTimestamp(value: string | null): string {
      return value ? new Date(value).toLocaleString('it-IT') : t('status.never')
    }

    async function refresh(): Promise<void> {
      await basketsStore.loadRegistry()
      await executionStore.load()
      await marketStore.load()
    }

    async function changeMode(value: unknown): Promise<void> {
      const outcome = await marketStore.setMode(String(value) as server.MarketManagerMode)

      if (outcome !== 'Applied') {
        ElMessage.error(t('market.modeFailed'))
      }
    }

    async function toggleAnalysis(isRunning: boolean): Promise<void> {
      const outcome = await marketStore.setAnalysisState(isRunning)

      if (outcome === 'Applied') {
        ElMessage.success(isRunning ? t('market.analysisStarted') : t('market.analysisStopped'))

        return
      }

      // The server refuses to start an analysis it could not judge, so the message names the missing input
      // instead of leaving the switch silently ineffective.
      ElMessage.warning(t('market.analysisRefused'))
    }

    async function openDetail(proposalId: string): Promise<void> {
      await marketStore.selectProposal(proposalId)
    }

    function requestDecision(payload: DecisionRequest): void {
      pendingDecision.value = payload
      decisionReason.value = ''
    }

    function closeDecision(): void {
      pendingDecision.value = null
      decisionReason.value = ''
    }

    async function confirmDecision(): Promise<void> {
      const pending = pendingDecision.value

      if (!pending) {
        return
      }

      const outcome = await marketStore.decide(
        pending.kind,
        pending.proposalId,
        decisionReason.value.trim(),
      )

      reportOutcome(pending.kind, outcome)
      closeDecision()
    }

    async function refreshDetail(): Promise<void> {
      if (marketStore.selectedProposalId) {
        await marketStore.selectProposal(marketStore.selectedProposalId)
      } else {
        await marketStore.load()
      }
    }

    /**
     * Starts the execution of the proposal in the drawer. The refusal reason travels back from the server, so
     * a missing stop distance or an engaged kill switch is reported instead of a generic failure.
     */
    async function startExecution(): Promise<void> {
      const pendingProposalId = marketStore.detail?.proposalId

      if (!pendingProposalId) {
        return
      }

      const outcome = await executionStore.start(pendingProposalId)

      if (outcome === 'Applied') {
        ElMessage.success(t('market.startExecutionDone'))
        await refresh()

        return
      }

      if (outcome === 'Refused') {
        const reason = executionStore.lastRefusalReason

        ElMessage.warning(
          reason
            ? t('market.startExecutionRefused') + ` (${reason})`
            : t('market.startExecutionRefused'),
        )

        return
      }

      if (outcome === 'NotFound') {
        ElMessage.error(t('market.outcome.NotFound'))

        return
      }

      ElMessage.error(t('market.startExecutionFailed'))
    }

    function reportOutcome(kind: ProposalDecisionKind, outcome: string): void {
      if (outcome === 'Applied') {
        ElMessage.success(t(`market.decided.${kind}`))

        return
      }

      if (outcome === 'NotDecidable') {
        ElMessage.warning(t('market.outcome.NotDecidable'))

        return
      }

      if (outcome === 'GateRegressed') {
        ElMessage.warning(t('market.outcome.GateRegressed'))

        return
      }

      if (outcome === 'Expired') {
        ElMessage.warning(t('market.outcome.Expired'))

        return
      }

      if (outcome === 'AlreadyDecided') {
        ElMessage.info(t('market.outcome.AlreadyDecided'))

        return
      }

      if (outcome === 'NotFound') {
        ElMessage.error(t('market.outcome.NotFound'))

        return
      }

      if (outcome === 'Refused') {
        ElMessage.warning(t('market.decisionRefused'))

        return
      }

      ElMessage.error(t('market.decisionFailed'))
    }

    onMounted(refresh)

    return {
      t,
      marketStore,
      basketsStore,
      executionStore,
      pendingDecision,
      decisionReason,
      modeOptions,
      currentMode,
      isAnalysisRunning,
      policyHintKey,
      activeBasketLabel,
      lastCycleKey,
      decisionForPanel,
      reasonIsRequired,
      confirmDisabled,
      canStartExecution,
      decisionDialogTitle,
      decisionDialogBody,
      formatTimestamp,
      refresh,
      changeMode,
      toggleAnalysis,
      openDetail,
      requestDecision,
      closeDecision,
      confirmDecision,
      refreshDetail,
      startExecution,
    }
  },
})
