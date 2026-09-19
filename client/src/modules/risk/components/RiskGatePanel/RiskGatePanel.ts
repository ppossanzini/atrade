import { computed, defineComponent, type PropType } from 'vue'
import { useI18n } from 'vue-i18n'

type TagType = 'success' | 'warning' | 'danger' | 'info'

const verdictTagTypes: Record<server.RiskGateVerdict, TagType> = {
  Allow: 'success',
  Review: 'warning',
  Block: 'danger',
}

/**
 * Presents the current risk decision for one basket: the aggregate verdict, the version and snapshot
 * it was evaluated on, and every gate with code, observed value, threshold and timestamp (AC-08).
 * It is presentational: the decision is read from the store by the view and nothing is derived here.
 */
export default defineComponent({
  name: 'RiskGatePanel',
  props: {
    decision: {
      type: Object as PropType<server.RiskDecision | null>,
      default: null,
    },
    isLoading: {
      type: Boolean,
      default: false,
    },
    hasLoadFailure: {
      type: Boolean,
      default: false,
    },
  },
  emits: ['refresh'],
  setup(props, { emit }) {
    const { t } = useI18n()

    const gates = computed(() => props.decision?.gates ?? [])

    const verdictTagType = computed<TagType>(() =>
      props.decision ? verdictTagTypes[props.decision.verdict] : 'info',
    )

    // The absence of a market snapshot is the reason most gates block today, so it is stated
    // explicitly instead of leaving the operator to infer it from the gate list.
    const hasSnapshot = computed(() => Boolean(props.decision?.snapshotCapturedAtUtc))

    function gateVerdictTagType(verdict: server.RiskGateVerdict): TagType {
      return verdictTagTypes[verdict]
    }

    /**
     * Element Plus evaluates a cell template once with an empty row while it builds the column, so a
     * missing value renders as empty text instead of producing a spurious i18n warning. Every real
     * gate carries both a code and a verdict, which is the vocabulary these two lookups resolve.
     */
    function gateVerdictLabel(verdict: server.RiskGateVerdict): string {
      return verdict ? t(`riskVerdict.${verdict}`) : ''
    }

    function gateCode(code: server.RiskGateCode): string {
      return code ? t(`riskGateCode.${code}`) : ''
    }

    function gateSubject(subject: string): string {
      return subject === 'basket' ? t('risk.basketSubject') : subject
    }

    function gateMarket(market: server.MarketKind | null): string {
      return market ? t(`marketKind.${market}`) : t('risk.notAvailable')
    }

    /**
     * Renders a measurement with its unit. A null measurement is a missing datum, which is shown as
     * such and never as a zero.
     */
    function formatMeasured(value: number | null, unit: string | null): string {
      if (value === null || value === undefined) {
        return t('risk.notAvailable')
      }

      const formatted = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 2 }).format(value)
      const unitLabel = unit ? t(`riskUnit.${unit}`) : ''

      return unitLabel ? `${formatted} ${unitLabel}` : formatted
    }

    function formatTimestamp(value: string | null): string {
      return value ? new Date(value).toLocaleString('it-IT') : t('risk.notAvailable')
    }

    function requestRefresh(): void {
      emit('refresh')
    }

    return {
      t,
      gates,
      verdictTagType,
      hasSnapshot,
      gateVerdictTagType,
      gateVerdictLabel,
      gateCode,
      gateSubject,
      gateMarket,
      formatMeasured,
      formatTimestamp,
      requestRefresh,
    }
  },
})
