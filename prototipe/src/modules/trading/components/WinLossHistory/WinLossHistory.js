import { useI18n } from 'vue-i18n'

export default {
  name: 'WinLossHistory',
  props: {
    episodes: { type: Array, required: true },
    metrics: { type: Array, required: true },
    strategies: { type: Array, required: true },
    outcome: { type: String, required: true },
    strategy: { type: String, required: true },
    selectedEpisode: { type: Object, default: null },
  },
  emits: ['update:outcome', 'update:strategy', 'reset', 'select'],
  setup() {
    const { t } = useI18n()
    const outcomeType = (outcome) => ({ win: 'success', loss: 'danger', breakeven: 'info' })[outcome]
    const formatCurrency = (value) => new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 }).format(value)
    const formatSigned = (value) => `${value > 0 ? '+' : ''}${value.toFixed(2)}`
    return { t, outcomeType, formatCurrency, formatSigned }
  },
}