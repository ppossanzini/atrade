import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

const initialLegs = [
  { id: 1, symbol: 'EURUSD', market: 'FX', selected: true, side: 'Long', weight: 35, timeframe: '15m', score: 82, correlation: 0.18, volatility: 7.2, spread: 0.7, riskCap: 0.35, status: 'eligible' },
  { id: 2, symbol: 'GBPUSD', market: 'FX', selected: true, side: 'Long', weight: 25, timeframe: '15m', score: 74, correlation: 0.46, volatility: 9.8, spread: 1.1, riskCap: 0.28, status: 'eligible' },
  { id: 3, symbol: 'USDJPY', market: 'FX', selected: false, side: 'Short', weight: 20, timeframe: '30m', score: 51, correlation: -0.61, volatility: 6.4, spread: 0.9, riskCap: 0.22, status: 'review' },
  { id: 4, symbol: 'XAUUSD', market: 'Metal', selected: true, side: 'Short', weight: 40, timeframe: '5m', score: 79, correlation: -0.22, volatility: 18.6, spread: 2.8, riskCap: 0.3, status: 'eligible' },
  { id: 5, symbol: 'US500', market: 'Index', selected: false, side: 'Long', weight: 15, timeframe: '1h', score: 43, correlation: 0.71, volatility: 12.1, spread: 0.5, riskCap: 0.18, status: 'blocked' },
]

const cloneLegs = (legs) => legs.map((leg) => ({ ...leg }))

const initialBaskets = [
  {
    id: 'macro-diversified',
    name: 'Macro Diversified',
    status: 'active',
    activeVersion: 3,
    latestVersion: 3,
    strategy: 'Regime + momentum',
    updatedAt: '18/09/2026 18:40',
    legs: cloneLegs(initialLegs),
    versions: [
      { version: 3, status: 'active', createdAt: '18/09/2026', note: 'Ridotta correlazione USD' },
      { version: 2, status: 'superseded', createdAt: '16/09/2026', note: 'Aggiunta copertura XAUUSD' },
      { version: 1, status: 'superseded', createdAt: '12/09/2026', note: 'Versione iniziale' },
    ],
  },
  {
    id: 'metals-hedge',
    name: 'Metals Hedge',
    status: 'inactive',
    activeVersion: 2,
    latestVersion: 2,
    strategy: 'Mean reversion',
    updatedAt: '18/09/2026 11:08',
    legs: initialLegs.map((leg) => ({
      ...leg,
      selected: ['USDJPY', 'XAUUSD'].includes(leg.symbol),
      weight: leg.symbol === 'XAUUSD' ? 65 : leg.symbol === 'USDJPY' ? 35 : leg.weight,
    })),
    versions: [
      { version: 2, status: 'published', createdAt: '15/09/2026', note: 'Hedge USDJPY al 35%' },
      { version: 1, status: 'superseded', createdAt: '10/09/2026', note: 'Versione iniziale' },
    ],
  },
  {
    id: 'usd-breakout',
    name: 'USD Breakout',
    status: 'archived',
    activeVersion: 5,
    latestVersion: 5,
    strategy: 'Momentum puro',
    updatedAt: '17/09/2026 14:12',
    legs: initialLegs.map((leg) => ({
      ...leg,
      selected: ['EURUSD', 'GBPUSD', 'USDJPY'].includes(leg.symbol),
      weight: leg.symbol === 'USDJPY' ? 40 : 30,
    })),
    versions: [
      { version: 5, status: 'archived', createdAt: '17/09/2026', note: 'Archiviato dopo perdita di edge' },
      { version: 4, status: 'superseded', createdAt: '14/09/2026', note: 'Filtro volatilità aggiornato' },
      { version: 3, status: 'superseded', createdAt: '11/09/2026', note: 'Soglia breakout ridotta' },
    ],
  },
]

export const usePrototypeStore = defineStore('prototype', () => {
  const activeSection = ref('basket')
  const baskets = ref(initialBaskets)
  const selectedBasketId = ref('macro-diversified')
  const activeBasketId = ref('macro-diversified')
  const failurePolicy = ref('minimum')
  const minimumCoverage = ref(75)
  const strategyMode = ref('regime')
  const riskPerBasket = ref(0.8)
  const dailyLossLimit = ref(2.5)
  const analysisRunning = ref(false)
  const simulationStatus = ref('idle')
  const lastSimulation = ref(null)
  const marketManagerMode = ref('supervised')
  const continuousAnalysis = ref(true)
  const marketProposals = ref([
    {
      id: 'SIG-1042',
      createdAt: '18:44:12',
      basket: 'Macro Diversified',
      version: 3,
      action: 'entry',
      direction: 'Long',
      confidence: 87,
      expectedRisk: 0.62,
      status: 'autoApproved',
      gate: 'approved',
      rationale: 'Regime risk-on, momentum concorde su tre gambe e spread entro soglia.',
      evidence: 18,
      expiresIn: '01:42',
    },
    {
      id: 'SIG-1041',
      createdAt: '18:42:38',
      basket: 'Macro Diversified',
      version: 3,
      action: 'reduce',
      direction: 'GBPUSD -30%',
      confidence: 74,
      expectedRisk: 0.18,
      status: 'needsReview',
      gate: 'review',
      rationale: 'Correlazione EURUSD/GBPUSD salita a 0,72; riduzione proposta per contenere la concentrazione USD.',
      evidence: 11,
      expiresIn: '04:18',
    },
    {
      id: 'SIG-1040',
      createdAt: '18:39:05',
      basket: 'Metals Hedge',
      version: 2,
      action: 'entry',
      direction: 'Short XAUUSD',
      confidence: 68,
      expectedRisk: 0.44,
      status: 'blocked',
      gate: 'blocked',
      rationale: 'Segnale valido, ma il paniere non è attivo e lo spread XAUUSD supera il limite statico.',
      evidence: 9,
      expiresIn: 'Scaduta',
    },
  ])

  const selectedBasket = computed(() => baskets.value.find((basket) => basket.id === selectedBasketId.value) ?? baskets.value[0])
  const activeBasket = computed(() => baskets.value.find((basket) => basket.id === activeBasketId.value))
  const basketVersion = computed(() => selectedBasket.value.activeVersion)
  const basketName = computed(() => selectedBasket.value.name)
  const legs = computed(() => selectedBasket.value.legs)
  const selectedLegs = computed(() => legs.value.filter((leg) => leg.selected))
  const totalWeight = computed(() => selectedLegs.value.reduce((sum, leg) => sum + leg.weight, 0))
  const weightedScore = computed(() => {
    if (!totalWeight.value) return 0
    return Math.round(selectedLegs.value.reduce((sum, leg) => sum + leg.score * leg.weight, 0) / totalWeight.value)
  })
  const riskState = computed(() => {
    if (totalWeight.value !== 100) return 'review'
    if (selectedLegs.value.some((leg) => leg.status === 'blocked')) return 'blocked'
    return 'approved'
  })

  function selectSection(section) {
    activeSection.value = section
  }

  function selectBasket(id) {
    if (baskets.value.some((basket) => basket.id === id)) selectedBasketId.value = id
  }

  function createBasket(name) {
    const id = `basket-${Date.now()}`
    baskets.value.push({
      id,
      name,
      status: 'inactive',
      activeVersion: 1,
      latestVersion: 1,
      strategy: 'Non assegnata',
      updatedAt: 'Ora',
      legs: initialLegs.map((leg) => ({ ...leg, selected: false, weight: 0 })),
      versions: [{ version: 1, status: 'draft', createdAt: 'Oggi', note: 'Bozza iniziale' }],
    })
    selectedBasketId.value = id
  }

  function renameBasket(name) {
    selectedBasket.value.name = name
    selectedBasket.value.updatedAt = 'Ora'
  }

  function cloneBasket(name) {
    const source = selectedBasket.value
    const id = `basket-${Date.now()}`
    baskets.value.push({
      ...source,
      id,
      name,
      status: 'inactive',
      activeVersion: 1,
      latestVersion: 1,
      updatedAt: 'Ora',
      legs: cloneLegs(source.legs),
      versions: [{ version: 1, status: 'draft', createdAt: 'Oggi', note: `Clonato da ${source.name} v${source.activeVersion}` }],
    })
    selectedBasketId.value = id
  }

  function archiveBasket() {
    if (selectedBasket.value.id === activeBasketId.value) return false
    selectedBasket.value.status = 'archived'
    const latestVersion = selectedBasket.value.versions.find((item) => item.version === selectedBasket.value.activeVersion)
    if (latestVersion) latestVersion.status = 'archived'
    selectedBasket.value.updatedAt = 'Ora'
    return true
  }

  function activateBasket() {
    if (selectedBasket.value.status === 'archived') return false
    const previous = activeBasket.value
    if (previous) {
      previous.status = 'inactive'
      previous.versions.forEach((item) => {
        if (item.status === 'active') item.status = 'published'
      })
    }
    selectedBasket.value.status = 'active'
    selectedBasket.value.versions.forEach((item) => {
      if (item.version === selectedBasket.value.activeVersion) item.status = 'active'
    })
    selectedBasket.value.updatedAt = 'Ora'
    activeBasketId.value = selectedBasket.value.id
    return true
  }

  function toggleLeg(id, selected) {
    const leg = legs.value.find((item) => item.id === id)
    if (leg) leg.selected = selected
  }

  function updateLeg(id, patch) {
    const leg = legs.value.find((item) => item.id === id)
    if (leg) Object.assign(leg, patch)
  }

  function normalizeWeights() {
    const total = totalWeight.value
    if (!total) return
    let allocated = 0
    selectedLegs.value.forEach((leg, index) => {
      const normalized = index === selectedLegs.value.length - 1
        ? 100 - allocated
        : Math.round((leg.weight / total) * 100)
      leg.weight = normalized
      allocated += normalized
    })
  }

  function publishVersion() {
    selectedBasket.value.latestVersion += 1
    selectedBasket.value.activeVersion = selectedBasket.value.latestVersion
    selectedBasket.value.updatedAt = 'Ora'
    selectedBasket.value.versions.unshift({
      version: selectedBasket.value.latestVersion,
      status: selectedBasket.value.id === activeBasketId.value ? 'active' : 'published',
      createdAt: 'Oggi',
      note: 'Configurazione pubblicata dalla UI',
    })
  }

  function runAnalysis() {
    analysisRunning.value = true
    window.setTimeout(() => {
      selectedBasket.value.legs = legs.value.map((leg) => ({
        ...leg,
        score: Math.min(95, leg.score + (leg.selected ? 2 : 1)),
      }))
      analysisRunning.value = false
    }, 900)
  }

  function simulateExecution(scenario) {
    simulationStatus.value = 'running'
    window.setTimeout(() => {
      const scenarios = {
        nominal: { coverage: 100, residual: 0, decision: 'approved', message: 'Tutte le gambe eseguite entro slippage.' },
        partial: { coverage: 76, residual: 4200, decision: 'approved', message: 'GBPUSD parziale; copertura minima rispettata.' },
        rejected: { coverage: 65, residual: 11800, decision: 'blocked', message: 'XAUUSD rifiutata; compensazione richiesta.' },
      }
      lastSimulation.value = scenarios[scenario]
      simulationStatus.value = 'complete'
    }, 700)
  }

  function updateProposalStatus(id, status) {
    const proposal = marketProposals.value.find((item) => item.id === id)
    if (proposal) proposal.status = status
  }

  return {
    activeSection,
    baskets,
    selectedBasketId,
    activeBasketId,
    selectedBasket,
    activeBasket,
    basketVersion,
    basketName,
    legs,
    failurePolicy,
    minimumCoverage,
    strategyMode,
    riskPerBasket,
    dailyLossLimit,
    analysisRunning,
    simulationStatus,
    lastSimulation,
    marketManagerMode,
    continuousAnalysis,
    marketProposals,
    selectedLegs,
    totalWeight,
    weightedScore,
    riskState,
    selectSection,
    selectBasket,
    createBasket,
    renameBasket,
    cloneBasket,
    archiveBasket,
    activateBasket,
    toggleLeg,
    updateLeg,
    normalizeWeights,
    publishVersion,
    runAnalysis,
    simulateExecution,
    updateProposalStatus,
  }
})