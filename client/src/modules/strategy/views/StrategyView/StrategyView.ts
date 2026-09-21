import { computed, defineComponent, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { useStrategyStore } from '@/stores/strategy'

/**
 * Tag type per requirement state. A requirement the application cannot decide is neutral on purpose: it is
 * neither met nor breached, and colouring it would read as a verdict nobody issued.
 */
const requirementTagTypes: Record<server.PromotionRequirementState, 'success' | 'danger' | 'info'> =
  {
    Satisfied: 'success',
    NotSatisfied: 'danger',
    NotVerifiable: 'info',
  }

export default defineComponent({
  name: 'StrategyView',
  setup() {
    const { t } = useI18n()
    const strategyStore = useStrategyStore()

    const entryMode = ref<server.EntryMode>('RegimeMomentum')
    const failurePolicy = ref<server.FailurePolicy>('MinimumCoverage')
    const minimumCoverage = ref(75)
    const riskPerBasket = ref(0.8)
    const dailyLossLimit = ref(2.5)

    const entryModeOptions = computed(() => [
      { label: t('entryMode.RegimeMomentum'), value: 'RegimeMomentum' },
      { label: t('entryMode.Momentum'), value: 'Momentum' },
      { label: t('entryMode.MeanReversion'), value: 'MeanReversion' },
    ])

    const failurePolicyOptions = computed(() => [
      { label: t('failurePolicy.MinimumCoverage'), value: 'MinimumCoverage' },
      { label: t('failurePolicy.AllOrNothing'), value: 'AllOrNothing' },
      { label: t('failurePolicy.RequireConfirmation'), value: 'RequireConfirmation' },
    ])

    /**
     * Copies the draft into the form. The form is not bound directly to the store: a rule set is saved as a
     * whole, so a half-typed number must never travel as if the operator had decided it.
     */
    function fillFromDraft(): void {
      const draft = strategyStore.rulesInPreparation

      if (!draft) {
        return
      }

      entryMode.value = draft.entryMode
      failurePolicy.value = draft.failurePolicy
      minimumCoverage.value = draft.minimumCoverage
      riskPerBasket.value = draft.riskPerBasket
      dailyLossLimit.value = draft.dailyLossLimit
    }

    watch(() => strategyStore.rulesInPreparation, fillFromDraft, { immediate: false })

    function formatNumber(value: number | null | undefined, digits: number): string {
      if (value === null || value === undefined) {
        return t('risk.notAvailable')
      }

      return new Intl.NumberFormat('it-IT', { maximumFractionDigits: digits }).format(value)
    }

    function requirementTag(
      state: server.PromotionRequirementState,
    ): 'success' | 'danger' | 'info' {
      return requirementTagTypes[state] ?? 'info'
    }

    async function refresh(): Promise<void> {
      await strategyStore.load()
      fillFromDraft()
    }

    async function save(): Promise<void> {
      const outcome = await strategyStore.saveRules({
        entryMode: entryMode.value,
        failurePolicy: failurePolicy.value,
        minimumCoverage: minimumCoverage.value,
        riskPerBasket: riskPerBasket.value,
        dailyLossLimit: dailyLossLimit.value,
      })

      if (outcome === 'Applied') {
        ElMessage.success(t('strategy.saved'))
        fillFromDraft()

        return
      }

      if (outcome === 'Refused') {
        ElMessage.warning(t('strategy.refused'))

        return
      }

      if (outcome === 'NotFound') {
        ElMessage.error(t('strategy.noActiveBasket'))

        return
      }

      ElMessage.error(t('strategy.saveFailed'))
    }

    onMounted(refresh)

    return {
      t,
      strategyStore,
      entryMode,
      failurePolicy,
      minimumCoverage,
      riskPerBasket,
      dailyLossLimit,
      entryModeOptions,
      failurePolicyOptions,
      formatNumber,
      requirementTag,
      refresh,
      save,
    }
  },
})
