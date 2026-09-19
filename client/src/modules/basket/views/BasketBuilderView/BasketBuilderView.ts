import { computed, defineComponent, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import BasketLegTable from '@/modules/basket/components/BasketLegTable/BasketLegTable.vue'
import RiskGatePanel from '@/modules/risk/components/RiskGatePanel/RiskGatePanel.vue'
import RiskLimitsPanel from '@/modules/risk/components/RiskLimitsPanel/RiskLimitsPanel.vue'
import { useBasketsStore, type BasketMutationOutcome } from '@/stores/baskets'
import { useRiskStore } from '@/stores/risk'

type DialogMode = 'create' | 'rename' | 'clone' | 'publish' | 'activate' | 'archive'

const defaultPolicy = (): server.BasketPolicy => ({
  failurePolicy: 'MinimumCoverage',
  minimumCoverage: 75,
  riskPerBasket: 0.8,
  dailyLossLimit: 2.5,
})

export default defineComponent({
  name: 'BasketBuilderView',
  components: { BasketLegTable, RiskGatePanel, RiskLimitsPanel },
  setup() {
    const { t } = useI18n()
    const basketsStore = useBasketsStore()
    const riskStore = useRiskStore()

    const draftLegs = ref<server.BasketCompositionLeg[]>([])
    const draftPolicy = ref<server.BasketPolicy>(defaultPolicy())
    const newLegSymbol = ref('')
    const versionNote = ref('')
    const dialogMode = ref<DialogMode | null>(null)
    const nameInput = ref('')
    const pendingVersionId = ref<string | null>(null)

    const detail = computed(() => basketsStore.detail)
    const versions = computed(() => basketsStore.versions)
    const isArchived = computed(() => detail.value?.status === 'Archived')
    const isReadOnly = computed(() => !detail.value || isArchived.value)

    const selectedLegs = computed(() => draftLegs.value.filter((leg) => leg.isSelected))
    const selectedWeight = computed(() =>
      selectedLegs.value.reduce((total, leg) => total + leg.weight, 0),
    )

    const weightsValid = computed(
      () =>
        selectedLegs.value.length > 0 &&
        selectedLegs.value.every((leg) => leg.weight > 0) &&
        selectedWeight.value === 100,
    )

    const policyValid = computed(
      () =>
        draftPolicy.value.minimumCoverage >= 50 &&
        draftPolicy.value.minimumCoverage <= 100 &&
        draftPolicy.value.riskPerBasket >= 0.1 &&
        draftPolicy.value.riskPerBasket <= 10 &&
        draftPolicy.value.dailyLossLimit >= 0.1 &&
        draftPolicy.value.dailyLossLimit <= 20,
    )

    const compositionIsDirty = computed(
      () => JSON.stringify(draftLegs.value) !== JSON.stringify(detail.value?.draftLegs ?? []),
    )

    const policyIsDirty = computed(
      () => JSON.stringify(draftPolicy.value) !== JSON.stringify(detail.value?.draftPolicy ?? null),
    )

    // A published version freezes what the server already holds, so an unsaved draft is a warning
    // rather than a blocker.
    const canSaveComposition = computed(
      () => !isReadOnly.value && compositionIsDirty.value && weightsValid.value,
    )

    const canSavePolicy = computed(
      () => !isReadOnly.value && policyIsDirty.value && policyValid.value,
    )

    const dialogTitleKeys: Record<DialogMode, string> = {
      create: 'basket.createTitle',
      rename: 'basket.renameTitle',
      clone: 'basket.cloneTitle',
      publish: 'basket.publishTitle',
      activate: 'basket.activateTitle',
      archive: 'basket.archiveTitle',
    }

    const dialogTitle = computed(() =>
      dialogMode.value ? t(dialogTitleKeys[dialogMode.value]) : '',
    )

    const isNameDialog = computed(
      () =>
        dialogMode.value === 'create' ||
        dialogMode.value === 'rename' ||
        dialogMode.value === 'clone',
    )

    const confirmDisabled = computed(() => isNameDialog.value && !nameInput.value.trim())

    watch(detail, (value) => {
      draftLegs.value = value ? value.draftLegs.map((leg) => ({ ...leg })) : []
      draftPolicy.value = value?.draftPolicy ? { ...value.draftPolicy } : defaultPolicy()
      versionNote.value = ''
    })

    // The decision is produced by the server from stored state, so it follows both the selected
    // basket and every stored change the basket store applies after a mutation.
    watch(
      () => [basketsStore.selectedBasketId, basketsStore.detail] as const,
      () => {
        void loadRisk()
      },
    )

    function statusTagType(status: server.BasketStatus): 'success' | 'info' | 'warning' {
      if (status === 'Active') {
        return 'success'
      }

      return status === 'Archived' ? 'warning' : 'info'
    }

    function versionTagType(
      status: server.BasketVersionStatus,
    ): 'success' | 'info' | 'warning' | 'danger' {
      if (status === 'Active') {
        return 'success'
      }

      return status === 'Archived' ? 'warning' : 'info'
    }

    function formatTimestamp(value: string | null): string {
      return value ? new Date(value).toLocaleString('it-IT') : t('status.never')
    }

    function reportNameOutcome(outcome: BasketMutationOutcome, successKey: string): void {
      if (outcome === 'Applied') {
        ElMessage.success(t(successKey))

        return
      }

      if (outcome === 'Conflict') {
        ElMessage.warning(t('basket.nameConflict'))

        return
      }

      if (outcome === 'InvalidInput') {
        ElMessage.warning(t('basket.nameInvalid'))

        return
      }

      if (outcome === 'NotFound') {
        ElMessage.error(t('basket.notFound'))

        return
      }

      ElMessage.error(t('basket.operationFailed'))
    }

    function addLeg(): void {
      const symbol = newLegSymbol.value.trim().toUpperCase()

      if (!symbol) {
        ElMessage.warning(t('basket.symbolRequired'))

        return
      }

      if (symbol.length > 32) {
        ElMessage.warning(t('basket.symbolTooLong'))

        return
      }

      if (draftLegs.value.some((leg) => leg.symbol.toUpperCase() === symbol)) {
        ElMessage.warning(t('basket.symbolDuplicated'))

        return
      }

      // A new leg starts excluded with no weight so it cannot corrupt the 100% total by accident.
      draftLegs.value = [
        ...draftLegs.value,
        {
          symbol,
          market: 'Fx',
          direction: 'Long',
          timeFrame: 'H1',
          weight: 0,
          riskCap: 0.5,

          // A new leg starts without a stop distance: the sizing refuses to size it until the operator
          // decides how far the leg may run against us.
          stopDistancePips: 0,
          isSelected: false,
        },
      ]

      newLegSymbol.value = ''
    }

    /**
     * Spreads the current relative weights across the selected legs so they total 100%. This only
     * prepares a valid request; the 100% rule itself stays a server-side rule.
     */
    function normalizeWeights(): void {
      const positions = draftLegs.value
        .map((leg, index) => (leg.isSelected ? index : -1))
        .filter((index) => index >= 0)

      if (positions.length === 0) {
        return
      }

      const total = positions.reduce((sum, index) => sum + (draftLegs.value[index]?.weight ?? 0), 0)
      const weights = positions.map((index) => {
        const currentWeight = draftLegs.value[index]?.weight ?? 0

        return total > 0
          ? Math.round((currentWeight / total) * 100)
          : Math.floor(100 / positions.length)
      })
      const drift = 100 - weights.reduce((sum, weight) => sum + weight, 0)

      // Rounding leaves a small drift: it is absorbed by the first selected leg so the total is
      // exactly 100% before the request is sent.
      weights[0] = (weights[0] ?? 0) + drift

      draftLegs.value = draftLegs.value.map((leg, index) => {
        const position = positions.indexOf(index)
        const weight = position >= 0 ? weights[position] : undefined

        return weight === undefined ? leg : { ...leg, weight }
      })
    }

    async function saveComposition(): Promise<void> {
      if (!compositionIsDirty.value) {
        ElMessage.info(t('basket.compositionNoChanges'))

        return
      }

      if (!weightsValid.value) {
        ElMessage.warning(t('basket.compositionInvalidWeights'))

        return
      }

      const outcome = await basketsStore.saveComposition(draftLegs.value)

      if (outcome === 'Applied') {
        ElMessage.success(t('basket.compositionSaved'))

        return
      }

      ElMessage.warning(
        outcome === 'Conflict'
          ? t('basket.compositionInvalidLegs')
          : t('basket.compositionInvalidWeights'),
      )
    }

    async function savePolicy(): Promise<void> {
      if (!policyIsDirty.value) {
        ElMessage.info(t('basket.policyNoChanges'))

        return
      }

      if (!policyValid.value) {
        ElMessage.warning(t('basket.policyInvalid'))

        return
      }

      const outcome = await basketsStore.savePolicy({ ...draftPolicy.value })

      if (outcome === 'Applied') {
        ElMessage.success(t('basket.policySaved'))

        return
      }

      ElMessage.warning(
        outcome === 'Conflict' ? t('basket.operationFailed') : t('basket.policyInvalid'),
      )
    }

    function openDialog(mode: DialogMode, versionId: string | null = null): void {
      dialogMode.value = mode
      pendingVersionId.value = versionId

      if (mode === 'rename') {
        nameInput.value = detail.value?.name ?? ''

        return
      }

      if (mode === 'clone') {
        nameInput.value = detail.value ? `${detail.value.name} copia` : ''

        return
      }

      nameInput.value = ''
    }

    function closeDialog(): void {
      dialogMode.value = null
      pendingVersionId.value = null
    }

    async function confirmDialog(): Promise<void> {
      const mode = dialogMode.value

      if (mode === 'publish') {
        await confirmPublish()

        return
      }

      if (mode === 'activate') {
        await confirmActivate()

        return
      }

      if (mode === 'archive') {
        await confirmArchive()

        return
      }

      await confirmNameOperation(mode)
    }

    async function confirmNameOperation(mode: DialogMode | null): Promise<void> {
      const name = nameInput.value.trim()

      if (!name) {
        ElMessage.warning(t('basket.nameRequired'))

        return
      }

      if (mode === 'create') {
        reportNameOutcome(await basketsStore.createBasket(name), 'basket.created')
      } else if (mode === 'rename') {
        reportNameOutcome(await basketsStore.renameSelectedBasket(name), 'basket.renamed')
      } else if (mode === 'clone') {
        reportNameOutcome(await basketsStore.cloneSelectedBasket(name), 'basket.cloned')
      }

      closeDialog()
    }

    async function confirmPublish(): Promise<void> {
      const outcome = await basketsStore.publishVersion(versionNote.value.trim())

      if (outcome === 'Applied') {
        ElMessage.success(t('basket.published'))
      } else if (outcome === 'Conflict') {
        ElMessage.warning(t('basket.publishedRejected'))
      } else {
        ElMessage.error(t('basket.operationFailed'))
      }

      closeDialog()
    }

    async function confirmActivate(): Promise<void> {
      if (!pendingVersionId.value) {
        closeDialog()

        return
      }

      const outcome = await basketsStore.activateVersion(pendingVersionId.value)

      if (outcome === 'Applied') {
        ElMessage.success(t('basket.activated'))
      } else if (outcome === 'Conflict') {
        ElMessage.warning(t('basket.activateRejected'))
      } else {
        ElMessage.error(t('basket.operationFailed'))
      }

      closeDialog()
    }

    async function confirmArchive(): Promise<void> {
      const outcome = await basketsStore.archiveSelectedBasket()

      if (outcome === 'Applied') {
        ElMessage.success(t('basket.archived'))
      } else if (outcome === 'Conflict') {
        ElMessage.warning(t('basket.archiveRejected'))
      } else {
        ElMessage.error(t('basket.operationFailed'))
      }

      closeDialog()
    }

    async function selectBasket(basketId: string): Promise<void> {
      await basketsStore.selectBasket(basketId)
    }

    async function loadRisk(): Promise<void> {
      await riskStore.load(basketsStore.selectedBasketId)
    }

    async function refresh(): Promise<void> {
      await basketsStore.loadRegistry()
      await loadRisk()
    }

    onMounted(refresh)

    return {
      t,
      basketsStore,
      riskStore,
      draftLegs,
      draftPolicy,
      newLegSymbol,
      versionNote,
      dialogMode,
      nameInput,
      detail,
      versions,
      isArchived,
      isReadOnly,
      selectedLegs,
      selectedWeight,
      weightsValid,
      policyValid,
      compositionIsDirty,
      policyIsDirty,
      canSaveComposition,
      canSavePolicy,
      dialogTitle,
      isNameDialog,
      confirmDisabled,
      statusTagType,
      versionTagType,
      formatTimestamp,
      addLeg,
      normalizeWeights,
      saveComposition,
      savePolicy,
      openDialog,
      closeDialog,
      confirmDialog,
      selectBasket,
      loadRisk,
      refresh,
    }
  },
})
