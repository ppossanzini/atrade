import { computed, defineComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import MetricRow from '@/modules/operations/components/MetricRow/MetricRow.vue'
import { useOperationsStore } from '@/stores/operations'

export default defineComponent({
  name: 'OperationalStatusView',
  components: { MetricRow },
  setup() {
    const { t } = useI18n()
    const operationsStore = useOperationsStore()
    const engageReason = ref('')

    const status = computed(() => operationsStore.status)
    const killSwitch = computed(() => operationsStore.status?.killSwitch ?? null)
    const account = computed(() => operationsStore.status?.account ?? null)
    const marketManager = computed(() => operationsStore.status?.marketManager ?? null)

    const evidenceStore = computed(() => operationsStore.status?.evidenceStore ?? null)
    const analysisModel = computed(() => operationsStore.status?.analysisModel ?? null)

    const isKillSwitchEngaged = computed(() => killSwitch.value?.isEngaged === true)
    const hasKillSwitchReason = computed(() => Boolean(killSwitch.value?.reason))

    const killSwitchTagType = computed(() => (isKillSwitchEngaged.value ? 'danger' : 'success'))

    const killSwitchStateKey = computed(() =>
      isKillSwitchEngaged.value ? 'status.engaged' : 'status.released',
    )

    const environmentKey = computed(() =>
      account.value ? `environment.${account.value.environment}` : 'status.never',
    )

    const connectionKey = computed(() =>
      account.value ? `connectionState.${account.value.connectionState}` : 'status.never',
    )

    const modeKey = computed(() =>
      marketManager.value ? `marketManagerMode.${marketManager.value.mode}` : 'status.never',
    )

    const analysisKey = computed(() => {
      if (!marketManager.value) {
        return 'status.never'
      }

      return marketManager.value.isAnalysisRunning
        ? 'status.analysisRunning'
        : 'status.analysisSuspended'
    })

    function formatTimestamp(value: string | null): string {
      return value ? new Date(value).toLocaleString('it-IT') : t('status.never')
    }

    async function refresh(): Promise<void> {
      await operationsStore.loadStatus()
    }

    async function engage(): Promise<void> {
      const outcome = await operationsStore.engageKillSwitch(engageReason.value)

      if (outcome === 'Applied') {
        engageReason.value = ''
        ElMessage.success(t('status.engagedDone'))

        return
      }

      ElMessage.error(t('auth.unexpectedError'))
    }

    async function release(): Promise<void> {
      const outcome = await operationsStore.releaseKillSwitch()

      if (outcome === 'Applied') {
        ElMessage.success(t('status.releasedDone'))

        return
      }

      if (outcome === 'Blocked') {
        ElMessage.warning(t('status.releaseBlocked'))

        return
      }

      ElMessage.error(t('auth.unexpectedError'))
    }

    onMounted(refresh)

    return {
      t,
      operationsStore,
      status,
      killSwitch,
      account,
      marketManager,
      evidenceStore,
      analysisModel,
      engageReason,
      isKillSwitchEngaged,
      hasKillSwitchReason,
      killSwitchTagType,
      killSwitchStateKey,
      environmentKey,
      connectionKey,
      modeKey,
      analysisKey,
      formatTimestamp,
      refresh,
      engage,
      release,
    }
  },
})
