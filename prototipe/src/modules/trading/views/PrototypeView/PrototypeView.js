import { computed, ref } from 'vue'
import { Check, CircleCheckFilled, CircleCloseFilled, Promotion, Refresh, VideoPlay } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import TradingShell from '@/layouts/TradingShell/TradingShell.vue'
import BasketLegTable from '@/modules/trading/components/BasketLegTable/BasketLegTable.vue'
import MarketManager from '@/modules/trading/components/MarketManager/MarketManager.vue'
import WinLossHistory from '@/modules/trading/components/WinLossHistory/WinLossHistory.vue'
import { usePrototypeStore } from '@/store/prototype'

export default {
  name: 'PrototypeView',
  components: { TradingShell, BasketLegTable, MarketManager, WinLossHistory, CircleCheckFilled, CircleCloseFilled },
  setup() {
    const { t } = useI18n()
    const store = usePrototypeStore()
    const selectedScenario = ref('partial')
    const historyOutcome = ref('all')
    const historyStrategy = ref('all')
    const basketDialog = ref(null)
    const basketNameInput = ref('')
    const equityBars = [31, 34, 32, 39, 42, 45, 43, 49, 54, 52, 58, 62, 59, 66, 71, 68, 76, 79, 82, 88]
    const dashboardMetrics = [
      { label: 'dashboard.netPnl', value: '+€2,438', note: 'dashboard.today', tone: 'positive' },
      { label: 'dashboard.activeRisk', value: '0.68%', note: 'dashboard.limitRisk', tone: 'neutral' },
      { label: 'dashboard.drawdown', value: '1.14%', note: 'dashboard.limitDrawdown', tone: 'neutral' },
      { label: 'dashboard.openBaskets', value: '1', note: 'dashboard.paperOnly', tone: 'warning' },
    ]
    const promotionSteps = [
      { label: 'strategy.stepDraft', note: 'strategy.stepDraftNote', type: 'success' },
      { label: 'strategy.stepBacktest', note: 'strategy.stepBacktestNote', type: 'success' },
      { label: 'strategy.stepShadow', note: 'strategy.stepShadowNote', type: 'warning' },
      { label: 'strategy.stepDemo', note: 'strategy.stepDemoNote', type: 'info', hollow: true },
    ]
    const riskChecks = computed(() => [
      { label: 'riskChecks.weight', value: `${store.totalWeight}%`, pass: store.totalWeight === 100 },
      { label: 'riskChecks.coverage', value: `${store.minimumCoverage}%`, pass: store.minimumCoverage >= 70 },
      { label: 'riskChecks.loss', value: `${store.dailyLossLimit}%`, pass: store.dailyLossLimit <= 3 },
      { label: 'riskChecks.stale', value: '184 ms', pass: true },
      { label: 'riskChecks.margin', value: '74%', pass: true },
    ])
    const journalEntries = [
      { time: '18:42:11', type: 'success', tag: 'success', kind: 'journal.decision', title: 'journal.entryOne', copy: 'journal.entryOneCopy', id: 'DEC-00A91', source: 'journal.localLlm', evidence: '18 evidenze' },
      { time: '18:41:48', type: 'warning', tag: 'warning', kind: 'journal.retrieval', title: 'journal.entryTwo', copy: 'journal.entryTwoCopy', id: 'RAG-0192C', source: 'journal.jigen', evidence: '0.87 similarità' },
      { time: '18:40:02', type: 'primary', tag: 'info', kind: 'journal.userAction', title: 'journal.entryThree', copy: 'journal.entryThreeCopy', id: 'CFG-003V3', source: 'journal.operator', evidence: 'v3' },
      { time: '18:37:19', type: 'danger', tag: 'danger', kind: 'journal.riskEvent', title: 'journal.entryFour', copy: 'journal.entryFourCopy', id: 'RSK-00D21', source: 'journal.staticRisk', evidence: '€11.800 residuo' },
    ]
    const historyEpisodes = [
      { id: 'EP-00241', closedAt: '18/09 · 16:42', basket: 'Macro Diversified', version: 'v3', strategy: 'Regime + momentum', outcome: 'win', pnl: 684, rMultiple: 1.42, duration: '2h 18m', mae: -0.31, mfe: 1.61, evidenceCount: 18, entryDecision: 'Regime risk-on confermato; score composito 81 e correlazione residua sotto soglia.', management: 'Ridotta GBPUSD del 30% dopo aumento correlazione con EURUSD.', exitReason: 'Target di paniere raggiunto con copertura completa.' },
      { id: 'EP-00240', closedAt: '18/09 · 11:08', basket: 'Metals Hedge', version: 'v2', strategy: 'Mean reversion', outcome: 'loss', pnl: -392, rMultiple: -0.81, duration: '47m', mae: -0.88, mfe: 0.19, evidenceCount: 11, entryDecision: 'Deviazione XAUUSD oltre 2 sigma con hedge USDJPY.', management: 'Stop invariato; nessuna evidenza sufficiente per mediare la posizione.', exitReason: 'Stop statico del paniere eseguito.' },
      { id: 'EP-00239', closedAt: '17/09 · 20:31', basket: 'Macro Diversified', version: 'v3', strategy: 'Regime + momentum', outcome: 'win', pnl: 518, rMultiple: 1.08, duration: '3h 04m', mae: -0.22, mfe: 1.22, evidenceCount: 16, entryDecision: 'Momentum concorde su tre gambe e liquidità nominale.', management: 'Trailing attivato dopo +0.7R; pesi invariati.', exitReason: 'Trailing stop composito.' },
      { id: 'EP-00238', closedAt: '17/09 · 14:12', basket: 'USD Breakout', version: 'v5', strategy: 'Momentum puro', outcome: 'breakeven', pnl: 24, rMultiple: 0.05, duration: '1h 12m', mae: -0.41, mfe: 0.48, evidenceCount: 9, entryDecision: 'Breakout confermato ma ampiezza inferiore alla mediana.', management: 'Portato stop a pareggio dopo perdita di accelerazione.', exitReason: 'Uscita temporale.' },
      { id: 'EP-00237', closedAt: '16/09 · 18:55', basket: 'Macro Diversified', version: 'v2', strategy: 'Regime + momentum', outcome: 'loss', pnl: -286, rMultiple: -0.59, duration: '38m', mae: -0.64, mfe: 0.11, evidenceCount: 14, entryDecision: 'Regime valido ma spread XAUUSD vicino alla soglia.', management: 'Gamba XAUUSD compensata dopo fill parziale.', exitReason: 'Circuit breaker per deterioramento spread.' },
      { id: 'EP-00236', closedAt: '16/09 · 10:24', basket: 'Metals Hedge', version: 'v2', strategy: 'Mean reversion', outcome: 'win', pnl: 447, rMultiple: 0.93, duration: '2h 41m', mae: -0.27, mfe: 1.05, evidenceCount: 12, entryDecision: 'Rientro nella banda con conferma del modello locale.', management: 'Presa parziale al 50% su +0.6R.', exitReason: 'Normalizzazione completata.' },
    ]
    const historyStrategies = [...new Set(historyEpisodes.map((episode) => episode.strategy))]
    const filteredEpisodes = computed(() => historyEpisodes.filter((episode) =>
      (historyOutcome.value === 'all' || episode.outcome === historyOutcome.value)
      && (historyStrategy.value === 'all' || episode.strategy === historyStrategy.value)))
    const selectedEpisode = ref(historyEpisodes[0])
    const historyMetrics = computed(() => {
      const wins = filteredEpisodes.value.filter((episode) => episode.outcome === 'win')
      const losses = filteredEpisodes.value.filter((episode) => episode.outcome === 'loss')
      const grossWin = wins.reduce((sum, episode) => sum + episode.pnl, 0)
      const grossLoss = Math.abs(losses.reduce((sum, episode) => sum + episode.pnl, 0))
      const pnl = filteredEpisodes.value.reduce((sum, episode) => sum + episode.pnl, 0)
      const averageR = filteredEpisodes.value.length ? filteredEpisodes.value.reduce((sum, episode) => sum + episode.rMultiple, 0) / filteredEpisodes.value.length : 0
      return [
        { label: 'history.winRate', value: `${Math.round((wins.length / Math.max(1, wins.length + losses.length)) * 100)}%`, note: 'history.closedTrades', tone: 'positive' },
        { label: 'history.profitFactor', value: grossLoss ? (grossWin / grossLoss).toFixed(2) : '—', note: 'history.grossRatio', tone: '' },
        { label: 'history.expectancy', value: `${averageR > 0 ? '+' : ''}${averageR.toFixed(2)}R`, note: 'history.perEpisode', tone: averageR >= 0 ? 'positive' : 'negative' },
        { label: 'history.netPnl', value: new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 }).format(pnl), note: 'history.afterCosts', tone: pnl >= 0 ? 'positive' : 'negative' },
      ]
    })
    const resetHistoryFilters = () => {
      historyOutcome.value = 'all'
      historyStrategy.value = 'all'
    }
    const riskTagType = computed(() => ({ approved: 'success', review: 'warning', blocked: 'danger' })[store.riskState])
    const simulationTagType = computed(() => store.simulationStatus === 'complete' ? 'success' : 'info')
    const simulationLabel = computed(() => store.simulationStatus === 'idle' ? t('execution.notRun') : t(`execution.${store.simulationStatus}`))
    const resultRiskType = computed(() => store.lastSimulation?.decision === 'blocked' ? 'danger' : store.lastSimulation ? 'success' : 'info')
    const resultRiskLabel = computed(() => store.lastSimulation ? t(`risk.${store.lastSimulation.decision}`) : t('risk.pending'))
    const executionPercentage = (index) => {
      if (!store.lastSimulation) return 0
      if (selectedScenario.value === 'partial' && index === 1) return 72
      if (selectedScenario.value === 'rejected' && index === store.selectedLegs.length - 1) return 0
      return 100
    }
    const executionStatus = (index) => selectedScenario.value === 'rejected' && index === store.selectedLegs.length - 1 && store.lastSimulation ? 'exception' : 'success'
    const executionLegLabel = (index) => {
      if (!store.lastSimulation) return t('execution.queued')
      if (selectedScenario.value === 'partial' && index === 1) return t('execution.partialFill')
      if (selectedScenario.value === 'rejected' && index === store.selectedLegs.length - 1) return t('execution.rejected')
      return t('execution.filled')
    }
    const publishBasket = () => {
      if (store.totalWeight !== 100) {
        ElMessage.warning(t('basket.invalidWeight'))
        return
      }
      store.publishVersion()
      ElMessage.success(t('basket.published', { version: store.basketVersion }))
    }
    const openBasketDialog = (action) => {
      basketDialog.value = action
      basketNameInput.value = action === 'rename'
        ? store.basketName
        : action === 'clone' ? `${store.basketName} copia` : ''
    }
    const confirmBasketDialog = () => {
      const action = basketDialog.value
      if (['create', 'rename', 'clone'].includes(action) && !basketNameInput.value.trim()) return
      if (action === 'create') store.createBasket(basketNameInput.value.trim())
      if (action === 'rename') store.renameBasket(basketNameInput.value.trim())
      if (action === 'clone') store.cloneBasket(basketNameInput.value.trim())
      if (action === 'archive') store.archiveBasket()
      if (action === 'activate') store.activateBasket()
      basketDialog.value = null
      ElMessage.success(t(`basket.${action}Success`))
    }
    const basketStatusType = (status) => ({ active: 'success', inactive: 'info', archived: 'warning' })[status]
    const versionStatusType = (status) => ({ active: 'success', published: 'primary', draft: 'info', archived: 'warning' })[status] ?? 'info'

    return {
      t, store, selectedScenario, equityBars, dashboardMetrics, promotionSteps, riskChecks, journalEntries,
      historyOutcome, historyStrategy, historyStrategies, filteredEpisodes, selectedEpisode, historyMetrics, resetHistoryFilters,
      riskTagType, simulationTagType, simulationLabel, resultRiskType, resultRiskLabel,
      executionPercentage, executionStatus, executionLegLabel, publishBasket,
      basketDialog, basketNameInput, openBasketDialog, confirmBasketDialog, basketStatusType, versionStatusType,
      Check, Promotion, Refresh, VideoPlay,
    }
  },
}