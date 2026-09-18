<script src="./PrototypeView.js"></script>

<template>
  <TradingShell>
    <section v-if="store.activeSection === 'dashboard'" class="workspace-page">
      <div class="page-heading">
        <div><span class="section-label">{{ t('dashboard.eyebrow') }}</span><h1>{{ t('dashboard.title') }}</h1></div>
        <el-tag :type="riskTagType" effect="dark">{{ t(`risk.${store.riskState}`) }}</el-tag>
      </div>
      <el-row :gutter="12" class="metric-grid">
        <el-col v-for="metric in dashboardMetrics" :key="metric.label" :xs="12" :sm="6">
          <el-card shadow="never" class="metric-card">
            <span class="section-label">{{ t(metric.label) }}</span>
            <strong class="metric-value data-value">{{ metric.value }}</strong>
            <small :class="metric.tone">{{ t(metric.note) }}</small>
          </el-card>
        </el-col>
      </el-row>
      <el-row :gutter="12" class="dashboard-grid">
        <el-col :xs="24" :lg="15">
          <el-card shadow="never" class="chart-panel">
            <template #header><div class="panel-title"><strong>{{ t('dashboard.equity') }}</strong><span class="data-value">€102,438</span></div></template>
            <div class="equity-chart" :aria-label="t('dashboard.equityAlt')"><i v-for="(height, index) in equityBars" :key="index" :style="{ height: `${height}%` }"></i></div>
            <div class="chart-axis"><span>09:00</span><span>12:00</span><span>15:00</span><span>18:00</span></div>
          </el-card>
        </el-col>
        <el-col :xs="24" :lg="9">
          <el-card shadow="never" class="exposure-panel">
            <template #header><strong>{{ t('dashboard.exposure') }}</strong></template>
            <div v-for="leg in store.selectedLegs" :key="leg.id" class="exposure-row">
              <div><strong class="data-value">{{ leg.symbol }}</strong><small>{{ leg.side }}</small></div>
              <el-progress :percentage="leg.weight" :show-text="false" :stroke-width="7" />
              <span class="data-value">{{ leg.weight }}%</span>
            </div>
          </el-card>
        </el-col>
      </el-row>
    </section>

    <section v-else-if="store.activeSection === 'basket'" class="workspace-page basket-page">
      <div class="page-heading">
        <div><span class="section-label">{{ t('basket.eyebrow') }}</span><h1>{{ t('basket.title') }}</h1><p>{{ t('basket.subtitle') }}</p></div>
        <div class="page-actions">
          <el-button :icon="Refresh" :loading="store.analysisRunning" :disabled="store.selectedBasket.status === 'archived'" @click="store.runAnalysis">{{ t('common.analyze') }}</el-button>
          <el-button type="primary" :icon="Promotion" :disabled="store.selectedBasket.status === 'archived'" @click="publishBasket">{{ t('common.publish') }}</el-button>
        </div>
      </div>
      <el-card shadow="never" class="basket-registry">
        <el-row :gutter="12" align="middle">
          <el-col :xs="24" :md="9">
            <span class="section-label">{{ t('basket.registry') }}</span>
            <el-select :model-value="store.selectedBasketId" @change="store.selectBasket">
              <el-option v-for="basket in store.baskets" :key="basket.id" :label="`${basket.name} · v${basket.activeVersion}`" :value="basket.id">
                <span>{{ basket.name }} · v{{ basket.activeVersion }}</span>
                <el-tag :type="basketStatusType(basket.status)" size="small" effect="plain">{{ t(`basket.status.${basket.status}`) }}</el-tag>
              </el-option>
            </el-select>
          </el-col>
          <el-col :xs="24" :md="7" class="basket-identity">
            <span class="section-label">{{ t('basket.selectedForEditing') }}</span>
            <strong>{{ store.basketName }} <span class="data-value">v{{ store.basketVersion }}</span></strong>
            <el-tag :type="basketStatusType(store.selectedBasket.status)" size="small">{{ t(`basket.status.${store.selectedBasket.status}`) }}</el-tag>
          </el-col>
          <el-col :xs="24" :md="8" class="basket-registry__actions">
            <el-button @click="openBasketDialog('create')">{{ t('basket.create') }}</el-button>
            <el-button :disabled="store.selectedBasket.status === 'archived'" @click="openBasketDialog('rename')">{{ t('basket.rename') }}</el-button>
            <el-button @click="openBasketDialog('clone')">{{ t('basket.clone') }}</el-button>
            <el-button type="primary" :disabled="store.selectedBasket.id === store.activeBasketId || store.selectedBasket.status === 'archived'" @click="openBasketDialog('activate')">{{ t('basket.activate') }}</el-button>
            <el-button type="warning" plain :disabled="store.selectedBasket.id === store.activeBasketId || store.selectedBasket.status === 'archived'" @click="openBasketDialog('archive')">{{ t('basket.archive') }}</el-button>
          </el-col>
        </el-row>
        <el-alert v-if="store.selectedBasket.id !== store.activeBasketId" :title="t('basket.editingInactive', { active: store.activeBasket.name })" type="warning" :closable="false" show-icon />
        <el-table :data="store.selectedBasket.versions" max-height="176" size="small" class="version-table">
          <el-table-column :label="t('basket.version')" width="92"><template #default="scope"><strong class="data-value">v{{ scope.row.version }}</strong></template></el-table-column>
          <el-table-column prop="createdAt" :label="t('basket.createdAt')" width="130" />
          <el-table-column :label="t('basket.versionState')" width="120"><template #default="scope"><el-tag :type="versionStatusType(scope.row.status)" size="small" effect="plain">{{ t(`basket.versionStatus.${scope.row.status}`) }}</el-tag></template></el-table-column>
          <el-table-column prop="note" :label="t('basket.note')" min-width="210" />
        </el-table>
      </el-card>
      <div class="basket-strip">
        <div><span class="section-label">{{ t('basket.selected') }}</span><strong class="data-value">{{ store.selectedLegs.length }}/{{ store.legs.length }}</strong></div>
        <div><span class="section-label">{{ t('basket.totalWeight') }}</span><strong class="data-value" :class="{ 'value-warning': store.totalWeight !== 100 }">{{ store.totalWeight }}%</strong></div>
        <div><span class="section-label">{{ t('basket.compositeScore') }}</span><strong class="data-value">{{ store.weightedScore }}/100</strong></div>
        <div><span class="section-label">{{ t('basket.riskGate') }}</span><el-tag :type="riskTagType" effect="light">{{ t(`risk.${store.riskState}`) }}</el-tag></div>
        <el-button text :disabled="store.totalWeight === 100 || store.selectedBasket.status === 'archived'" @click="store.normalizeWeights">{{ t('common.normalize') }}</el-button>
      </div>
      <div class="panel basket-table-panel"><BasketLegTable :legs="store.legs" :readonly="store.selectedBasket.status === 'archived'" @toggle="store.toggleLeg" @update="store.updateLeg" /></div>
      <el-row :gutter="12" class="basket-lower-grid">
        <el-col :xs="24" :lg="14">
          <el-card shadow="never" class="analysis-card">
            <template #header><div class="panel-title"><strong>{{ t('basket.analysisTitle') }}</strong><el-tag type="success" effect="plain">JigenDB · 18 {{ t('basket.cases') }}</el-tag></div></template>
            <div class="analysis-lead"><span class="analysis-score data-value">82</span><div><strong>{{ t('basket.analysisHeadline') }}</strong><p>{{ t('basket.analysisCopy') }}</p></div></div>
            <div class="evidence-list"><span><i class="evidence-dot evidence-dot--good"></i>{{ t('basket.evidenceOne') }}</span><span><i class="evidence-dot evidence-dot--warn"></i>{{ t('basket.evidenceTwo') }}</span><span><i class="evidence-dot evidence-dot--good"></i>{{ t('basket.evidenceThree') }}</span></div>
          </el-card>
        </el-col>
        <el-col :xs="24" :lg="10">
          <el-card shadow="never" class="policy-card">
            <template #header><strong>{{ t('basket.failurePolicy') }}</strong></template>
            <el-form label-position="top">
              <el-form-item :label="t('basket.policy')"><el-select v-model="store.failurePolicy"><el-option :label="t('policies.minimum')" value="minimum" /><el-option :label="t('policies.allOrNothing')" value="all" /><el-option :label="t('policies.confirm')" value="confirm" /></el-select></el-form-item>
              <el-form-item :label="t('basket.coverage')"><el-slider v-model="store.minimumCoverage" :min="50" :max="100" show-input /></el-form-item>
            </el-form>
          </el-card>
        </el-col>
      </el-row>

      <el-dialog :model-value="Boolean(basketDialog)" :title="t(`basket.dialog.${basketDialog || 'create'}`)" width="min(32rem, 92vw)" @close="basketDialog = null">
        <el-form v-if="['create', 'rename', 'clone'].includes(basketDialog)" label-position="top">
          <el-form-item :label="t('basket.name')"><el-input v-model="basketNameInput" maxlength="60" show-word-limit /></el-form-item>
        </el-form>
        <el-alert v-else-if="basketDialog === 'archive'" :title="t('basket.archiveWarning')" type="warning" :closable="false" show-icon />
        <div v-else-if="basketDialog === 'activate'" class="activation-impact">
          <el-alert :title="t('basket.activationWarning')" type="warning" :closable="false" show-icon />
          <el-row :gutter="12">
            <el-col :span="12"><span class="section-label">{{ t('basket.currentActive') }}</span><strong>{{ store.activeBasket.name }} · v{{ store.activeBasket.activeVersion }}</strong><small>{{ store.activeBasket.strategy }}</small></el-col>
            <el-col :span="12"><span class="section-label">{{ t('basket.nextActive') }}</span><strong>{{ store.basketName }} · v{{ store.basketVersion }}</strong><small>{{ store.selectedBasket.strategy }}</small></el-col>
          </el-row>
        </div>
        <template #footer><el-button @click="basketDialog = null">{{ t('basket.cancel') }}</el-button><el-button type="primary" :disabled="['create', 'rename', 'clone'].includes(basketDialog) && !basketNameInput.trim()" @click="confirmBasketDialog">{{ t('basket.confirm') }}</el-button></template>
      </el-dialog>
    </section>

    <section v-else-if="store.activeSection === 'strategy'" class="workspace-page">
      <div class="page-heading"><div><span class="section-label">{{ t('strategy.eyebrow') }}</span><h1>{{ t('strategy.title') }}</h1><p>{{ t('strategy.subtitle') }}</p></div><el-button type="primary" :icon="Check">{{ t('strategy.validate') }}</el-button></div>
      <el-row :gutter="12" class="strategy-grid">
        <el-col :xs="24" :lg="14"><el-card shadow="never" class="strategy-form-card"><template #header><div class="panel-title"><strong>{{ t('strategy.rules') }}</strong><el-tag effect="plain">DSL v0.3</el-tag></div></template><el-form label-position="top"><el-form-item :label="t('strategy.mode')"><el-select v-model="store.strategyMode"><el-option :label="t('strategy.regime')" value="regime" /><el-option :label="t('strategy.momentum')" value="momentum" /><el-option :label="t('strategy.meanReversion')" value="mean" /></el-select></el-form-item><el-row :gutter="12"><el-col :span="12"><el-form-item :label="t('strategy.riskBasket')"><el-input-number v-model="store.riskPerBasket" :min="0.1" :max="2" :step="0.1" /></el-form-item></el-col><el-col :span="12"><el-form-item :label="t('strategy.dailyLoss')"><el-input-number v-model="store.dailyLossLimit" :min="0.5" :max="5" :step="0.5" /></el-form-item></el-col></el-row><el-alert :title="t('strategy.guardrail')" type="warning" :closable="false" show-icon /></el-form></el-card></el-col>
        <el-col :xs="24" :lg="10"><el-card shadow="never" class="promotion-card"><template #header><strong>{{ t('strategy.promotion') }}</strong></template><el-timeline><el-timeline-item v-for="step in promotionSteps" :key="step.label" :type="step.type" :hollow="step.hollow"><strong>{{ t(step.label) }}</strong><small>{{ t(step.note) }}</small></el-timeline-item></el-timeline></el-card></el-col>
      </el-row>
    </section>

    <MarketManager
      v-else-if="store.activeSection === 'market'"
      :proposals="store.marketProposals"
      :mode="store.marketManagerMode"
      :continuous-analysis="store.continuousAnalysis"
      :active-basket="store.activeBasket"
      @update:mode="store.marketManagerMode = $event"
      @update:continuous-analysis="store.continuousAnalysis = $event"
      @update-status="store.updateProposalStatus"
    />

    <section v-else-if="store.activeSection === 'execution'" class="workspace-page">
      <div class="page-heading"><div><span class="section-label">{{ t('execution.eyebrow') }}</span><h1>{{ t('execution.title') }}</h1><p>{{ t('execution.subtitle') }}</p></div></div>
      <div class="execution-toolbar panel"><el-select v-model="selectedScenario" :aria-label="t('execution.scenario')"><el-option :label="t('scenarios.nominal')" value="nominal" /><el-option :label="t('scenarios.partial')" value="partial" /><el-option :label="t('scenarios.rejected')" value="rejected" /></el-select><div><span class="section-label">{{ t('execution.policy') }}</span><strong>{{ t(`policies.${store.failurePolicy === 'all' ? 'allOrNothing' : store.failurePolicy}`) }}</strong></div><div><span class="section-label">{{ t('execution.targetCoverage') }}</span><strong class="data-value">{{ store.minimumCoverage }}%</strong></div><el-button type="primary" :icon="VideoPlay" :loading="store.simulationStatus === 'running'" @click="store.simulateExecution(selectedScenario)">{{ t('common.run') }}</el-button></div>
      <el-row :gutter="12" class="execution-grid">
        <el-col :xs="24" :lg="15"><el-card shadow="never" class="execution-card"><template #header><div class="panel-title"><strong>{{ t('execution.legs') }}</strong><el-tag :type="simulationTagType">{{ simulationLabel }}</el-tag></div></template><div v-for="(leg, index) in store.selectedLegs" :key="leg.id" class="execution-leg"><span class="execution-order data-value">0{{ index + 1 }}</span><div><strong class="data-value">{{ leg.symbol }}</strong><small>{{ leg.side }} · {{ leg.weight }}%</small></div><el-progress :percentage="executionPercentage(index)" :status="executionStatus(index)" :stroke-width="8" /><el-tag :type="executionStatus(index) === 'exception' ? 'danger' : 'success'" effect="plain" size="small">{{ executionLegLabel(index) }}</el-tag></div></el-card></el-col>
        <el-col :xs="24" :lg="9"><el-card shadow="never" class="risk-card"><template #header><div class="panel-title"><strong>{{ t('execution.riskGate') }}</strong><el-tag :type="resultRiskType" effect="dark">{{ resultRiskLabel }}</el-tag></div></template><div v-for="checkItem in riskChecks" :key="checkItem.label" class="risk-check"><el-icon :class="checkItem.pass ? 'risk-pass' : 'risk-fail'"><CircleCheckFilled v-if="checkItem.pass" /><CircleCloseFilled v-else /></el-icon><span>{{ t(checkItem.label) }}</span><strong class="data-value">{{ checkItem.value }}</strong></div><el-alert v-if="store.lastSimulation" :title="store.lastSimulation.message" :type="store.lastSimulation.decision === 'blocked' ? 'error' : 'success'" :closable="false" show-icon /></el-card></el-col>
      </el-row>
    </section>

    <section v-else-if="store.activeSection === 'history'" class="workspace-page">
      <div class="page-heading"><div><span class="section-label">{{ t('history.eyebrow') }}</span><h1>{{ t('history.title') }}</h1><p>{{ t('history.subtitle') }}</p></div><el-tag effect="plain">{{ t('history.mockNotice') }}</el-tag></div>
      <WinLossHistory
        :episodes="filteredEpisodes"
        :metrics="historyMetrics"
        :strategies="historyStrategies"
        :outcome="historyOutcome"
        :strategy="historyStrategy"
        :selected-episode="selectedEpisode"
        @update:outcome="historyOutcome = $event"
        @update:strategy="historyStrategy = $event"
        @reset="resetHistoryFilters"
        @select="selectedEpisode = $event"
      />
    </section>

    <section v-else class="workspace-page">
      <div class="page-heading"><div><span class="section-label">{{ t('journal.eyebrow') }}</span><h1>{{ t('journal.title') }}</h1><p>{{ t('journal.subtitle') }}</p></div><el-tag effect="plain">JigenDB embedded</el-tag></div>
      <el-card shadow="never" class="journal-card"><el-timeline><el-timeline-item v-for="entry in journalEntries" :key="entry.time" :timestamp="entry.time" placement="top" :type="entry.type"><div class="journal-entry"><div><el-tag :type="entry.tag" effect="plain" size="small">{{ t(entry.kind) }}</el-tag><strong>{{ t(entry.title) }}</strong></div><p>{{ t(entry.copy) }}</p><div class="journal-meta"><span class="data-value">{{ entry.id }}</span><span>{{ t(entry.source) }}</span><span>{{ entry.evidence }}</span></div></div></el-timeline-item></el-timeline></el-card>
    </section>
  </TradingShell>
</template>

<style scoped lang="less" src="./PrototypeView.less"></style>