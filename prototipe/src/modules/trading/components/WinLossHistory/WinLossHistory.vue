<script src="./WinLossHistory.js"></script>

<template>
  <div class="win-loss-history">
    <el-row :gutter="12" class="win-loss-history__metrics">
      <el-col v-for="metric in metrics" :key="metric.label" :xs="12" :sm="6">
        <el-card shadow="never" class="result-metric">
          <span class="section-label">{{ t(metric.label) }}</span>
          <strong class="data-value" :class="metric.tone">{{ metric.value }}</strong>
          <small>{{ t(metric.note) }}</small>
        </el-card>
      </el-col>
    </el-row>

    <div class="win-loss-history__filters panel">
      <el-select :model-value="outcome" :aria-label="t('history.outcomeFilter')" @change="$emit('update:outcome', $event)">
        <el-option :label="t('history.allOutcomes')" value="all" />
        <el-option :label="t('history.wins')" value="win" />
        <el-option :label="t('history.losses')" value="loss" />
        <el-option :label="t('history.breakeven')" value="breakeven" />
      </el-select>
      <el-select :model-value="strategy" :aria-label="t('history.strategyFilter')" @change="$emit('update:strategy', $event)">
        <el-option :label="t('history.allStrategies')" value="all" />
        <el-option v-for="name in strategies" :key="name" :label="name" :value="name" />
      </el-select>
      <span class="win-loss-history__count data-value">{{ t('history.results', { count: episodes.length }) }}</span>
      <el-button text @click="$emit('reset')">{{ t('history.resetFilters') }}</el-button>
    </div>

    <el-row :gutter="12" class="win-loss-history__content">
      <el-col :xs="24" :lg="16">
        <div class="panel win-loss-history__table">
          <el-table :data="episodes" height="100%" size="small" highlight-current-row row-key="id" @current-change="$emit('select', $event)">
            <el-table-column prop="closedAt" :label="t('history.closedAt')" width="132" />
            <el-table-column :label="t('history.basket')" min-width="160">
              <template #default="scope"><strong>{{ scope.row.basket }}</strong><small class="table-secondary">{{ scope.row.version }}</small></template>
            </el-table-column>
            <el-table-column prop="strategy" :label="t('history.strategy')" min-width="140" />
            <el-table-column :label="t('history.outcome')" width="104">
              <template #default="scope"><el-tag :type="outcomeType(scope.row.outcome)" effect="light" size="small">{{ t(`history.${scope.row.outcome}`) }}</el-tag></template>
            </el-table-column>
            <el-table-column :label="t('history.pnl')" width="104" sortable prop="pnl">
              <template #default="scope"><strong class="data-value" :class="scope.row.pnl >= 0 ? 'positive' : 'negative'">{{ formatCurrency(scope.row.pnl) }}</strong></template>
            </el-table-column>
            <el-table-column :label="t('history.rMultiple')" width="82" sortable prop="rMultiple">
              <template #default="scope"><span class="data-value">{{ formatSigned(scope.row.rMultiple) }}R</span></template>
            </el-table-column>
            <el-table-column prop="duration" :label="t('history.duration')" width="88" />
          </el-table>
        </div>
      </el-col>

      <el-col :xs="24" :lg="8">
        <el-card v-if="selectedEpisode" shadow="never" class="episode-detail">
          <template #header>
            <div class="episode-detail__header">
              <div><span class="section-label">{{ selectedEpisode.id }}</span><strong>{{ selectedEpisode.basket }}</strong></div>
              <el-tag :type="outcomeType(selectedEpisode.outcome)" effect="dark">{{ t(`history.${selectedEpisode.outcome}`) }}</el-tag>
            </div>
          </template>
          <div class="episode-detail__result">
            <div><span>{{ t('history.netResult') }}</span><strong class="data-value" :class="selectedEpisode.pnl >= 0 ? 'positive' : 'negative'">{{ formatCurrency(selectedEpisode.pnl) }}</strong></div>
            <div><span>{{ t('history.maxAdverse') }}</span><strong class="data-value">{{ selectedEpisode.mae }}%</strong></div>
            <div><span>{{ t('history.maxFavorable') }}</span><strong class="data-value">{{ selectedEpisode.mfe }}%</strong></div>
          </div>
          <div class="episode-detail__section"><span class="section-label">{{ t('history.entryDecision') }}</span><p>{{ selectedEpisode.entryDecision }}</p></div>
          <div class="episode-detail__section"><span class="section-label">{{ t('history.intermediateManagement') }}</span><p>{{ selectedEpisode.management }}</p></div>
          <div class="episode-detail__section"><span class="section-label">{{ t('history.exitReason') }}</span><p>{{ selectedEpisode.exitReason }}</p></div>
          <div class="episode-detail__rag"><span>JigenDB</span><strong class="data-value">{{ selectedEpisode.evidenceCount }} {{ t('history.evidences') }}</strong></div>
        </el-card>
        <el-empty v-else :description="t('history.selectEpisode')" />
      </el-col>
    </el-row>
  </div>
</template>

<style scoped lang="less" src="./WinLossHistory.less"></style>