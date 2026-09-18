import { useI18n } from 'vue-i18n'

export default {
  name: 'BasketLegTable',
  props: {
    legs: { type: Array, required: true },
    readonly: { type: Boolean, default: false },
  },
  emits: ['toggle', 'update'],
  setup() {
    const { t } = useI18n()
    const timeframes = ['5m', '15m', '30m', '1h', '4h']
    const formatSigned = (value) => `${value > 0 ? '+' : ''}${value.toFixed(2)}`
    const scoreClass = (score) => ({ 'score--good': score >= 70, 'score--weak': score < 55 })
    const statusType = (status) => ({ eligible: 'success', review: 'warning', blocked: 'danger' })[status]
    return { t, timeframes, formatSigned, scoreClass, statusType }
  },
}