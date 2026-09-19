import { defineComponent, type PropType } from 'vue'
import { useI18n } from 'vue-i18n'

/**
 * Presents the risk settings that still live in deployment configuration: the snapshot validity window.
 * The leg limits are not shown here because they no longer belong to the deployment: they travel with the
 * leg, so they are read and edited in the basket, where the decision was taken.
 *
 * A null window is rendered as not configured and never as a default: the engine blocks on it, so hiding
 * the gap would make a blocking verdict unexplainable from the UI.
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
  setup() {
    const { t } = useI18n()

    /**
     * Renders the window with its unit. A missing value is reported as not configured, which is the
     * actionable state, instead of an empty cell.
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
      formatThreshold,
    }
  },
})
