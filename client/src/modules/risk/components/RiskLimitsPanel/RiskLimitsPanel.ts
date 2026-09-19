import { computed, defineComponent, type PropType } from 'vue'
import { useI18n } from 'vue-i18n'

/**
 * Presents the risk thresholds actually in force, one block per market. A null threshold is rendered
 * as not configured and never as a default: the engine blocks on an unconfigured limit, so hiding the
 * gap would make a blocking verdict unexplainable from the UI.
 */
export default defineComponent({
  name: 'RiskLimitsPanel',
  props: {
    limits: {
      type: Object as PropType<server.RiskLimits | null>,
      default: null,
    },
    isLoading: {
      type: Boolean,
      default: false,
    },
  },
  setup(props) {
    const { t } = useI18n()

    const markets = computed(() => props.limits?.markets ?? [])

    /**
     * Renders a threshold with its unit. A missing threshold is reported as not configured, which is
     * the actionable state, instead of an empty cell.
     */
    function formatThreshold(value: number | null, unit: string): string {
      if (value === null || value === undefined) {
        return t('risk.notConfigured')
      }

      const formatted = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 2 }).format(value)

      return `${formatted} ${t(`riskUnit.${unit}`)}`
    }

    return {
      t,
      markets,
      formatThreshold,
    }
  },
})
