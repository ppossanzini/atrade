<script lang="ts" src="./MarketManagerView.ts"></script>

<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-header__title">{{ t('market.title') }}</h1>
        <p class="page-header__subtitle">{{ t('market.subtitle') }}</p>
      </div>

      <div class="inline-actions">
        <el-tag :type="isAnalysisRunning ? 'success' : 'info'" effect="dark">
          {{ t(isAnalysisRunning ? 'market.analysisRunning' : 'market.analysisPaused') }}
        </el-tag>
        <el-button :loading="marketStore.isLoading" @click="refresh">
          {{ t('market.refresh') }}
        </el-button>
      </div>
    </div>

    <el-alert
      v-if="marketStore.hasLoadFailure"
      class="market-alert"
      type="error"
      :closable="false"
      show-icon
      :title="t('common.sessionExpired')"
    />

    <div class="panel market-panel">
      <div class="market-controls">
        <div class="market-controls__block">
          <span class="panel-title">{{ t('market.operatingMode') }}</span>
          <el-segmented
            :model-value="currentMode"
            :options="modeOptions"
            :disabled="marketStore.isSaving"
            @change="changeMode($event)"
          />
        </div>

        <div class="market-controls__block">
          <span class="panel-title">{{ t('market.backgroundEngine') }}</span>
          <el-switch
            :model-value="isAnalysisRunning"
            :active-text="t('market.enabled')"
            :inactive-text="t('market.paused')"
            :loading="marketStore.isSaving"
            @change="toggleAnalysis(Boolean($event))"
          />
        </div>

        <div class="market-controls__block">
          <span class="panel-title">{{ t('market.executionBasket') }}</span>
          <strong>{{ activeBasketLabel }}</strong>
          <small class="market-controls__note">
            {{ t('market.lastCycle') }}: {{ lastCycleKey }}
          </small>
        </div>
      </div>

      <el-alert
        class="market-alert"
        type="warning"
        :closable="false"
        show-icon
        :title="t(policyHintKey)"
      />
    </div>

    <div class="market-metrics">
      <div class="panel market-metric">
        <span class="panel-title">{{ t('market.metrics.generated') }}</span>
        <strong class="market-metric__value">{{ marketStore.proposals.length }}</strong>
        <small>{{ t('market.metrics.currentQueue') }}</small>
      </div>
      <div class="panel market-metric">
        <span class="panel-title">{{ t('market.metrics.automatic') }}</span>
        <strong class="market-metric__value">{{ marketStore.autoApprovedCount }}</strong>
        <small>{{ t('market.metrics.withinPolicy') }}</small>
      </div>
      <div class="panel market-metric">
        <span class="panel-title">{{ t('market.metrics.attention') }}</span>
        <strong class="market-metric__value">{{ marketStore.attentionCount }}</strong>
        <small>{{ t('market.metrics.operatorRequired') }}</small>
      </div>
      <div class="panel market-metric">
        <span class="panel-title">{{ t('market.metrics.blocked') }}</span>
        <strong class="market-metric__value">{{ marketStore.blockedCount }}</strong>
        <small>{{ t('market.metrics.byGate') }}</small>
      </div>
    </div>

    <div class="panel market-panel">
      <div class="panel-header">
        <span class="panel-title">{{ t('market.queueTitle') }}</span>
        <span class="market-queue__subtitle">{{ t('market.queueSubtitle') }}</span>
      </div>

      <ProposalQueue
        :proposals="marketStore.proposals"
        :is-loading="marketStore.isLoading"
        :is-saving="marketStore.isSaving"
        @select="openDetail"
        @decide="requestDecision"
      />
    </div>

    <el-drawer
      :model-value="marketStore.detail !== null"
      :title="t('market.detailTitle')"
      size="min(34rem, 94vw)"
      @close="marketStore.closeDetail"
    >
      <div v-if="marketStore.detail" class="market-detail">
        <div class="market-detail__heading">
          <div>
            <span class="panel-title">{{ marketStore.detail.proposalId.slice(0, 8) }}</span>
            <strong
              >{{ marketStore.detail.basketName }} · v{{ marketStore.detail.versionNumber }}</strong
            >
          </div>
          <el-tag :type="marketStore.detail.isDecidable ? 'success' : 'info'" effect="plain">
            {{ t(marketStore.detail.isDecidable ? 'market.decidable' : 'market.notDecidable') }}
          </el-tag>
        </div>

        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.action') }}</span>
          <span class="metric-row__value">
            {{ t(`proposalAction.${marketStore.detail.action}`) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.status') }}</span>
          <span class="metric-row__value">
            {{ t(`proposalStatus.${marketStore.detail.status}`) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('strategy.mode') }}</span>
          <span class="metric-row__value">
            {{ t(`entryMode.${marketStore.detail.entryMode}`) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.confidence') }}</span>
          <span class="metric-row__value">{{ marketStore.detail.confidence }} %</span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.risk') }}</span>
          <span class="metric-row__value">
            {{ marketStore.detail.expectedRiskPercent.toFixed(2) }} %
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.proposedAt') }}</span>
          <span class="metric-row__value">
            {{ formatTimestamp(marketStore.detail.proposedAtUtc) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.expiry') }}</span>
          <span class="metric-row__value">
            {{ formatTimestamp(marketStore.detail.expiresAtUtc) }}
          </span>
        </div>
        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.decidedAt') }}</span>
          <span class="metric-row__value">
            {{ formatTimestamp(marketStore.detail.decidedAtUtc) }}
          </span>
        </div>

        <div class="metric-row">
          <span class="metric-row__label">{{ t('market.decisionReason') }}</span>
          <span class="metric-row__value">
            {{ marketStore.detail.decisionReason || t('risk.notAvailable') }}
          </span>
        </div>

        <span class="panel-title panel-title--spaced">{{ t('market.legs') }}</span>
        <el-table :data="marketStore.detail.legs" size="small">
          <el-table-column :label="t('basket.symbol')" min-width="110" prop="symbol" />
          <el-table-column :label="t('basket.market')" width="96">
            <template #default="scope">{{ t(`marketKind.${scope.row.market}`) }}</template>
          </el-table-column>
          <el-table-column :label="t('basket.direction')" width="96">
            <template #default="scope">{{ t(`legDirection.${scope.row.direction}`) }}</template>
          </el-table-column>
          <el-table-column :label="t('basket.weight')" width="96" align="right">
            <template #default="scope">{{ scope.row.weight }} %</template>
          </el-table-column>
          <el-table-column :label="t('basket.riskCap')" width="96" align="right">
            <template #default="scope">{{ scope.row.riskCap.toFixed(2) }}</template>
          </el-table-column>
        </el-table>

        <div class="metric-row panel-title--spaced">
          <span class="metric-row__label">{{ t('market.rationale') }}</span>
          <span class="metric-row__value market-detail__rationale">
            {{ marketStore.detail.rationale }}
          </span>
        </div>

        <RiskGatePanel
          class="market-detail__gates"
          :decision="decisionForPanel"
          :is-loading="marketStore.isLoading"
          :has-load-failure="false"
          @refresh="refreshDetail"
        />

        <div class="inline-actions market-detail__actions">
          <el-button
            :disabled="!marketStore.detail.isDecidable || marketStore.isSaving"
            @click="requestDecision({ kind: 'suspend', proposalId: marketStore.detail.proposalId })"
          >
            {{ t('market.suspend') }}
          </el-button>
          <el-button
            type="danger"
            plain
            :disabled="!marketStore.detail.isDecidable || marketStore.isSaving"
            @click="requestDecision({ kind: 'reject', proposalId: marketStore.detail.proposalId })"
          >
            {{ t('market.reject') }}
          </el-button>
          <el-button
            type="primary"
            plain
            :loading="executionStore.isSaving"
            :disabled="!canStartExecution || executionStore.isSaving"
            @click="startExecution"
          >
            {{ t('market.startExecution') }}
          </el-button>
          <el-button
            type="success"
            :disabled="!marketStore.detail.isDecidable || marketStore.isSaving"
            @click="requestDecision({ kind: 'approve', proposalId: marketStore.detail.proposalId })"
          >
            {{ t('market.approve') }}
          </el-button>
        </div>
      </div>
    </el-drawer>

    <el-dialog
      :model-value="pendingDecision !== null"
      :title="decisionDialogTitle"
      width="min(32rem, 92vw)"
      @close="closeDecision"
    >
      <el-alert
        class="market-alert"
        :type="pendingDecision?.kind === 'reject' ? 'error' : 'warning'"
        :closable="false"
        show-icon
        :title="decisionDialogBody"
      />

      <el-form label-position="top">
        <el-form-item :label="t('market.decisionReason')">
          <el-input
            v-model="decisionReason"
            type="textarea"
            :rows="3"
            maxlength="256"
            show-word-limit
            :placeholder="
              reasonIsRequired
                ? t('market.decisionReasonPlaceholder')
                : t('market.decisionReasonOptional')
            "
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="closeDecision">{{ t('common.cancel') }}</el-button>
        <el-button
          type="primary"
          :loading="marketStore.isSaving"
          :disabled="confirmDisabled"
          @click="confirmDecision"
        >
          {{ t('common.confirm') }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped lang="less" src="./MarketManagerView.less"></style>
