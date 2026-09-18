import { computed, ref } from 'vue'
import { Check, CloseBold, VideoPause, View } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

export default {
  name: 'MarketManager',
  props: {
    proposals: { type: Array, required: true },
    mode: { type: String, required: true },
    continuousAnalysis: { type: Boolean, required: true },
    activeBasket: { type: Object, required: true },
  },
  emits: ['update:mode', 'update:continuousAnalysis', 'update-status'],
  setup(props, { emit }) {
    const { t } = useI18n()
    const selectedProposal = ref(null)
    const pendingAction = ref(null)
    const modeOptions = computed(() => [
      { label: t('market.modes.manual'), value: 'manual' },
      { label: t('market.modes.supervised'), value: 'supervised' },
      { label: t('market.modes.automatic'), value: 'automatic' },
    ])
    const metrics = computed(() => [
      { label: 'market.metrics.analyzed', value: '1.284', note: 'market.metrics.lastHour', tone: '' },
      {
        label: 'market.metrics.generated',
        value: props.proposals.length,
        note: 'market.metrics.currentQueue',
        tone: '',
      },
      {
        label: 'market.metrics.automatic',
        value: props.proposals.filter((item) => item.status === 'autoApproved').length,
        note: 'market.metrics.withinPolicy',
        tone: 'positive',
      },
      {
        label: 'market.metrics.attention',
        value: props.proposals.filter((item) => item.status === 'needsReview').length,
        note: 'market.metrics.operatorRequired',
        tone: 'warning',
      },
    ])
    const statusType = (status) => ({
      autoApproved: 'success',
      needsReview: 'warning',
      blocked: 'danger',
      approved: 'success',
      rejected: 'danger',
      suspended: 'info',
    })[status] ?? 'info'
    const gateType = (gate) => ({ approved: 'success', review: 'warning', blocked: 'danger' })[gate] ?? 'info'
    const actionType = (action) => action === 'entry' ? 'success' : 'warning'
    const canDecide = (proposal) => proposal.status === 'needsReview'
    const openDetails = (proposal) => {
      selectedProposal.value = proposal
    }
    const requestAction = (proposal, action) => {
      pendingAction.value = { proposal, action }
    }
    const confirmAction = () => {
      emit('update-status', pendingAction.value.proposal.id, pendingAction.value.action)
      if (selectedProposal.value?.id === pendingAction.value.proposal.id) {
        selectedProposal.value = { ...selectedProposal.value, status: pendingAction.value.action }
      }
      pendingAction.value = null
    }
    return {
      t,
      selectedProposal,
      pendingAction,
      modeOptions,
      metrics,
      statusType,
      gateType,
      actionType,
      canDecide,
      openDetails,
      requestAction,
      confirmAction,
      Check,
      CloseBold,
      VideoPause,
      View,
    }
  },
}