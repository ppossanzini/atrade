<script lang="ts" src="./RiskLimitsPanel.ts"></script>

<template>
  <div class="risk-limits-panel">
    <div class="panel-header">
      <span class="panel-title">{{ t('risk.limitsTitle') }}</span>

      <el-tag
        v-if="limits"
        :type="limits.isConfigured ? 'success' : 'warning'"
        size="small"
        effect="plain"
      >
        {{ limits.isConfigured ? t('risk.configured') : t('risk.notConfigured') }}
      </el-tag>
    </div>

    <el-skeleton v-if="isLoading && !limits" :rows="2" animated />

    <p v-else-if="!limits" class="panel-empty">{{ t('risk.limitsUnavailable') }}</p>

    <template v-else>
      <div class="metric-row">
        <span class="metric-row__label">{{ t('risk.snapshotMaxAge') }}</span>
        <span
          class="metric-row__value"
          :class="{ 'metric-row__value--muted': limits.snapshotMaxAgeSeconds === null }"
        >
          {{ formatThreshold(limits.snapshotMaxAgeSeconds, 'seconds') }}
        </span>
      </div>

      <el-alert
        v-if="!limits.isConfigured"
        class="panel-alert"
        type="warning"
        :closable="false"
        show-icon
        :title="t('risk.limitsIncompleteHint')"
      />

      <p class="risk-limits-panel__note">{{ t('risk.legLimitsElsewhere') }}</p>
    </template>
  </div>
</template>

<style scoped lang="less" src="./RiskLimitsPanel.less"></style>
