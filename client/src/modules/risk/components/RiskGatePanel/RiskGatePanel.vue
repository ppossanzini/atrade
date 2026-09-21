<script lang="ts" src="./RiskGatePanel.ts"></script>

<template>
  <div class="risk-gate-panel">
    <div class="panel-header">
      <span class="panel-title">{{ t('risk.gateTitle') }}</span>

      <div class="inline-actions">
        <el-tag v-if="decision" :type="verdictTagType" effect="dark">
          {{ t(`riskVerdict.${decision.verdict}`) }}
        </el-tag>
        <el-button link :loading="isLoading" @click="requestRefresh">
          {{ t('risk.refresh') }}
        </el-button>
      </div>
    </div>

    <el-alert
      v-if="hasLoadFailure"
      class="panel-alert"
      type="error"
      :closable="false"
      show-icon
      :title="t('common.sessionExpired')"
    />

    <p v-if="!decision" class="panel-empty">{{ t('risk.noDecision') }}</p>

    <template v-else>
      <div class="metric-row">
        <span class="metric-row__label">{{ t('risk.evaluatedVersion') }}</span>
        <span class="metric-row__value">v{{ decision.versionNumber }}</span>
      </div>

      <div class="metric-row">
        <span class="metric-row__label">{{ t('risk.snapshotCapturedAt') }}</span>
        <span class="metric-row__value" :class="{ 'metric-row__value--muted': !hasSnapshot }">
          {{ hasSnapshot ? formatTimestamp(decision.snapshotCapturedAtUtc) : t('risk.noSnapshot') }}
        </span>
      </div>

      <div class="metric-row">
        <span class="metric-row__label">{{ t('risk.evaluatedAt') }}</span>
        <span class="metric-row__value">{{ formatTimestamp(decision.evaluatedAtUtc) }}</span>
      </div>

      <el-alert
        v-if="!hasSnapshot"
        class="panel-alert"
        type="warning"
        :closable="false"
        show-icon
        :title="t('risk.snapshotMissingHint')"
      />

      <span class="panel-title panel-title--spaced">{{ t('risk.gatesTitle') }}</span>

      <el-table :data="gates" :empty-text="t('risk.noGates')" size="small">
        <el-table-column :label="t('risk.gateVerdict')" width="126">
          <template #default="scope">
            <el-tag :type="gateVerdictTagType(scope.row.verdict)" size="small" effect="plain">
              {{ gateVerdictLabel(scope.row.verdict) }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column :label="t('risk.gateCode')" min-width="200">
          <template #default="scope">{{ gateCode(scope.row.code) }}</template>
        </el-table-column>

        <el-table-column :label="t('risk.gateSubject')" min-width="120">
          <template #default="scope">{{ gateSubject(scope.row.subject) }}</template>
        </el-table-column>

        <el-table-column :label="t('risk.gateObserved')" width="150" align="right">
          <template #default="scope">
            {{ formatMeasured(scope.row.observedValue, scope.row.unit) }}
          </template>
        </el-table-column>

        <el-table-column :label="t('risk.gateThreshold')" width="150" align="right">
          <template #default="scope">
            {{ formatMeasured(scope.row.thresholdValue, scope.row.unit) }}
          </template>
        </el-table-column>

        <el-table-column :label="t('risk.gateEvaluatedAt')" width="158">
          <template #default="scope">{{ formatTimestamp(scope.row.evaluatedAtUtc) }}</template>
        </el-table-column>

        <el-table-column :label="t('risk.gateDetail')" min-width="260">
          <template #default="scope">{{ scope.row.detail }}</template>
        </el-table-column>
      </el-table>
    </template>
  </div>
</template>

<style scoped lang="less" src="./RiskGatePanel.less"></style>
