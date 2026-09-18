import { DataAnalysis, Document, Grid, Monitor, Operation, PieChart, SwitchButton, TrendCharts, VideoPause } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { usePrototypeStore } from '@/store/prototype'

export default {
  name: 'TradingShell',
  setup() {
    const { t } = useI18n()
    const store = usePrototypeStore()
    const navigation = [
      { id: 'dashboard', label: 'navigation.dashboard', icon: PieChart },
      { id: 'basket', label: 'navigation.basket', icon: Grid },
      { id: 'strategy', label: 'navigation.strategy', icon: DataAnalysis },
      { id: 'market', label: 'navigation.market', icon: Monitor },
      { id: 'execution', label: 'navigation.execution', icon: Operation },
      { id: 'history', label: 'navigation.history', icon: TrendCharts },
      { id: 'journal', label: 'navigation.journal', icon: Document },
    ]
    return { t, store, navigation, SwitchButton, VideoPause }
  },
}