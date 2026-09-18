<script src="./MarketManager.js"></script>

<template>
  <section class="market-manager workspace-page">
    <div class="page-heading">
      <div>
        <span class="section-label">{{ t('market.eyebrow') }}</span>
        <h1>{{ t('market.title') }}</h1>
        <p>{{ t('market.subtitle') }}</p>
      </div>
      <el-tag :type="continuousAnalysis ? 'success' : 'info'" effect="dark">
        {{ t(continuousAnalysis ? 'market.analysisRunning' : 'market.analysisPaused') }}
      </el-tag>
    </div>

    <el-card shadow="never" class="market-manager__control-panel">
      <div class="market-manager__controls">
        <div class="market-manager__mode">
          <span class="section-label">{{ t('market.operatingMode') }}</span>
          <el-segmented :model-value="mode" :options="modeOptions" @change="$emit('update:mode', $event)" />
        </div>
        <div class="market-manager__heartbeat">
          <span class="market-manager__pulse" :class="{ 'market-manager__pulse--paused': !continuousAnalysis }"></span>
          <div>
            <strong>{{ t('market.backgroundEngine') }}</strong>
            <small>{{ t('market.lastCycle') }}</small>
          </div>
          <el-switch
            :model-value="continuousAnalysis"
            :active-text="t('market.enabled')"
            :inactive-text="t('market.paused')"
            @change="$emit('update:continuousAnalysis', $event)"
          />
        </div>
        <div class="market-manager__active-basket">
          <span class="section-label">{{ t('market.executionBasket') }}</span>
          <strong>{{ activeBasket.name }} · v{{ activeBasket.activeVersion }}</strong>
          <small>{{ activeBasket.strategy }}</small>
        </div>
      </div>
      <el-alert :title="t(`market.policy.${mode}`)" type="warning" :closable="false" show-icon />
    </el-card>

    <div class="market-manager__metrics">
      <el-card v-for="metric in metrics" :key="metric.label" shadow="never" class="metric-card">
        <span class="section-label">{{ t(metric.label) }}</span>
        <strong class="metric-value data-value">{{ metric.value }}</strong>
        <small :class="metric.tone">{{ t(metric.note) }}</small>
      </el-card>
    </div>

    <el-card shadow="never" class="market-manager__queue">
      <template #header>
        <div class="panel-title">
          <div>
            <strong>{{ t('market.queueTitle') }}</strong>
            <small>{{ t('market.queueSubtitle') }}</small>
          </div>
          <el-tag effect="plain">{{ t('market.proposalsCount', { count: proposals.length }) }}</el-tag>
        </div>
      </template>
      <el-table :data="proposals" row-key="id" size="small" @row-click="openDetails">
        <el-table-column prop="createdAt" :label="t('market.time')" width="92" />
        <el-table-column :label="t('market.proposal')" min-width="190">
          <template #default="scope">
            <div class="market-manager__proposal-cell">
              <strong>{{ scope.row.basket }} · v{{ scope.row.version }}</strong>
              <small class="data-value">{{ scope.row.id }} · {{ scope.row.direction }}</small>
            </div>
          </template>
        </el-table-column>
        <el-table-column :label="t('market.action')" width="106">
          <template #default="scope">
            <el-tag :type="actionType(scope.row.action)" effect="plain" size="small">
              {{ t(`market.actions.${scope.row.action}`) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="t('market.confidence')" width="124">
          <template #default="scope"><el-progress :percentage="scope.row.confidence" :stroke-width="7" /></template>
        </el-table-column>
        <el-table-column :label="t('market.risk')" width="88">
          <template #default="scope">
            <strong class="data-value">{{ scope.row.expectedRisk.toFixed(2) }}%</strong>
          </template>
        </el-table-column>
        <el-table-column :label="t('market.gate')" width="112">
          <template #default="scope">
            <el-tag :type="gateType(scope.row.gate)" size="small">
              {{ t(`risk.${scope.row.gate}`) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="t('market.status')" width="142">
          <template #default="scope">
            <el-tag :type="statusType(scope.row.status)" effect="plain" size="small">
              {{ t(`market.statuses.${scope.row.status}`) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="t('market.commands')" width="160" fixed="right">
          <template #default="scope">
            <div class="market-manager__commands" @click.stop>
              <el-tooltip :content="t('market.viewDetails')">
                <el-button :icon="View" :aria-label="t('market.viewDetails')" circle size="small" @click="openDetails(scope.row)" />
              </el-tooltip>
              <el-tooltip :content="t('market.approve')">
                <el-button :icon="Check" :aria-label="t('market.approve')" type="success" circle size="small" :disabled="!canDecide(scope.row)" @click="requestAction(scope.row, 'approved')" />
              </el-tooltip>
              <el-tooltip :content="t('market.suspend')">
                <el-button :icon="VideoPause" :aria-label="t('market.suspend')" circle size="small" :disabled="!canDecide(scope.row)" @click="requestAction(scope.row, 'suspended')" />
              </el-tooltip>
              <el-tooltip :content="t('market.reject')">
                <el-button :icon="CloseBold" :aria-label="t('market.reject')" type="danger" plain circle size="small" :disabled="!canDecide(scope.row)" @click="requestAction(scope.row, 'rejected')" />
              </el-tooltip>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-drawer :model-value="Boolean(selectedProposal)" :title="t('market.detailTitle')" size="min(30rem, 92vw)" @close="selectedProposal = null">
      <div v-if="selectedProposal" class="market-manager__detail">
        <div class="market-manager__detail-heading">
          <div>
            <span class="section-label">{{ selectedProposal.id }}</span>
            <strong>{{ selectedProposal.basket }} · v{{ selectedProposal.version }}</strong>
          </div>
          <el-tag :type="statusType(selectedProposal.status)">
            {{ t(`market.statuses.${selectedProposal.status}`) }}
          </el-tag>
        </div>
        <div class="market-manager__detail-metrics">
          <div>
            <span>{{ t('market.confidence') }}</span>
            <strong class="data-value">{{ selectedProposal.confidence }}%</strong>
          </div>
          <div>
            <span>{{ t('market.risk') }}</span>
            <strong class="data-value">{{ selectedProposal.expectedRisk.toFixed(2) }}%</strong>
          </div>
          <div>
            <span>{{ t('market.expiry') }}</span>
            <strong class="data-value">{{ selectedProposal.expiresIn }}</strong>
          </div>
        </div>
        <div class="market-manager__reasoning">
          <span class="section-label">{{ t('market.rationale') }}</span>
          <p>{{ selectedProposal.rationale }}</p>
        </div>
        <el-alert :title="t('market.evidenceSummary', { count: selectedProposal.evidence })" type="info" :closable="false" show-icon />
        <div class="market-manager__drawer-actions">
          <el-button :disabled="!canDecide(selectedProposal)" @click="requestAction(selectedProposal, 'suspended')">
            {{ t('market.suspend') }}
          </el-button>
          <el-button type="danger" plain :disabled="!canDecide(selectedProposal)" @click="requestAction(selectedProposal, 'rejected')">
            {{ t('market.reject') }}
          </el-button>
          <el-button type="success" :disabled="!canDecide(selectedProposal)" @click="requestAction(selectedProposal, 'approved')">
            {{ t('market.approve') }}
          </el-button>
        </div>
      </div>
    </el-drawer>

    <el-dialog :model-value="Boolean(pendingAction)" :title="t('market.confirmTitle')" width="min(30rem, 92vw)" @close="pendingAction = null">
      <el-alert
        v-if="pendingAction"
        :title="t(`market.confirm.${pendingAction.action}`, { id: pendingAction.proposal.id })"
        :type="pendingAction.action === 'rejected' ? 'error' : 'warning'"
        :closable="false"
        show-icon
      />
      <template #footer>
        <el-button @click="pendingAction = null">{{ t('basket.cancel') }}</el-button>
        <el-button type="primary" @click="confirmAction">{{ t('basket.confirm') }}</el-button>
      </template>
    </el-dialog>
  </section>
</template>

<style scoped lang="less" src="./MarketManager.less"></style>