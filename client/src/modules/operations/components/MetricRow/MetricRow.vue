<script lang="ts" src="./MetricRow.ts"></script>

<template>
  <div class="metric-row">
    <span class="metric-row__label">{{ t(labelKey) }}</span>
    <span class="metric-row__value" :class="{ 'metric-row__value--muted': muted }">
      {{ displayValue }}
    </span>
  </div>
</template>

<style scoped lang="less" src="./MetricRow.less"></style>
