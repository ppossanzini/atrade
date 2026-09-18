<script src="./BasketLegTable.js"></script>

<template>
  <el-table :data="legs" height="100%" size="small" row-key="id" class="basket-leg-table">
    <el-table-column width="54" align="center">
      <template #default="scope">
        <el-switch
          :model-value="scope.row.selected"
          :disabled="readonly"
          :aria-label="t('basket.includeLeg', { symbol: scope.row.symbol })"
          @change="$emit('toggle', scope.row.id, $event)"
        />
      </template>
    </el-table-column>
    <el-table-column :label="t('basket.symbol')" min-width="122">
      <template #default="scope">
        <div class="symbol-cell">
          <strong class="data-value">{{ scope.row.symbol }}</strong>
          <small>{{ scope.row.market }}</small>
        </div>
      </template>
    </el-table-column>
    <el-table-column :label="t('basket.signal')" width="96">
      <template #default="scope">
        <el-tag :type="scope.row.side === 'Long' ? 'success' : 'danger'" effect="plain" size="small">
          {{ scope.row.side }}
        </el-tag>
      </template>
    </el-table-column>
    <el-table-column :label="t('basket.score')" width="96" sortable prop="score">
      <template #default="scope">
        <span class="score data-value" :class="scoreClass(scope.row.score)">{{ scope.row.score }}</span>
      </template>
    </el-table-column>
    <el-table-column :label="t('basket.correlation')" width="104" sortable prop="correlation">
      <template #default="scope"><span class="data-value">{{ formatSigned(scope.row.correlation) }}</span></template>
    </el-table-column>
    <el-table-column :label="t('basket.volatility')" width="104" sortable prop="volatility">
      <template #default="scope"><span class="data-value">{{ scope.row.volatility.toFixed(1) }}%</span></template>
    </el-table-column>
    <el-table-column :label="t('basket.timeframe')" width="112">
      <template #default="scope">
        <el-select :model-value="scope.row.timeframe" :disabled="readonly" size="small" @change="$emit('update', scope.row.id, { timeframe: $event })">
          <el-option v-for="period in timeframes" :key="period" :label="period" :value="period" />
        </el-select>
      </template>
    </el-table-column>
    <el-table-column :label="t('basket.weight')" width="142">
      <template #default="scope">
        <el-input-number
          :model-value="scope.row.weight"
          :disabled="readonly || !scope.row.selected"
          :min="0"
          :max="100"
          :step="5"
          size="small"
          controls-position="right"
          @change="$emit('update', scope.row.id, { weight: $event })"
        />
      </template>
    </el-table-column>
    <el-table-column :label="t('basket.riskCap')" width="112">
      <template #default="scope"><span class="data-value">{{ scope.row.riskCap.toFixed(2) }}%</span></template>
    </el-table-column>
    <el-table-column :label="t('basket.state')" width="106">
      <template #default="scope">
        <el-tag :type="statusType(scope.row.status)" effect="light" size="small">{{ t(`status.${scope.row.status}`) }}</el-tag>
      </template>
    </el-table-column>
  </el-table>
</template>

<style scoped lang="less" src="./BasketLegTable.less"></style>