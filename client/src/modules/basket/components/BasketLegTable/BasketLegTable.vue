<script lang="ts" src="./BasketLegTable.ts"></script>

<template>
  <el-table :data="legs" :empty-text="t('basket.noLegs')" size="small" class="basket-leg-table">
    <el-table-column :label="t('basket.include')" width="76" align="center">
      <template #default="scope">
        <el-switch
          :model-value="scope.row.isSelected"
          :disabled="disabled"
          @change="patchLeg(scope.$index, { isSelected: Boolean($event) })"
        />
      </template>
    </el-table-column>

    <el-table-column :label="t('basket.symbol')" min-width="144">
      <template #default="scope">
        <el-input
          :model-value="scope.row.symbol"
          :disabled="disabled"
          size="small"
          maxlength="32"
          @update:model-value="patchLeg(scope.$index, { symbol: String($event).toUpperCase() })"
        />
      </template>
    </el-table-column>

    <el-table-column :label="t('basket.market')" width="132">
      <template #default="scope">
        <el-select
          :model-value="scope.row.market"
          :disabled="disabled"
          size="small"
          @update:model-value="patchLeg(scope.$index, { market: $event })"
        >
          <el-option
            v-for="market in markets"
            :key="market"
            :label="t(`marketKind.${market}`)"
            :value="market"
          />
        </el-select>
      </template>
    </el-table-column>

    <el-table-column :label="t('basket.direction')" width="122">
      <template #default="scope">
        <el-select
          :model-value="scope.row.direction"
          :disabled="disabled"
          size="small"
          @update:model-value="patchLeg(scope.$index, { direction: $event })"
        >
          <el-option
            v-for="direction in directions"
            :key="direction"
            :label="t(`legDirection.${direction}`)"
            :value="direction"
          />
        </el-select>
      </template>
    </el-table-column>

    <el-table-column :label="t('basket.timeFrame')" width="106">
      <template #default="scope">
        <el-select
          :model-value="scope.row.timeFrame"
          :disabled="disabled"
          size="small"
          @update:model-value="patchLeg(scope.$index, { timeFrame: $event })"
        >
          <el-option v-for="frame in timeFrames" :key="frame" :label="frame" :value="frame" />
        </el-select>
      </template>
    </el-table-column>

    <el-table-column :label="t('basket.weight')" width="152">
      <template #default="scope">
        <el-input-number
          :model-value="scope.row.weight"
          :disabled="disabled || !scope.row.isSelected"
          :min="0"
          :max="100"
          :step="5"
          size="small"
          controls-position="right"
          @update:model-value="patchLeg(scope.$index, { weight: Number($event) })"
        />
      </template>
    </el-table-column>

    <el-table-column :label="t('basket.riskCap')" width="152">
      <template #default="scope">
        <el-input-number
          :model-value="scope.row.riskCap"
          :disabled="disabled"
          :min="0.05"
          :max="5"
          :step="0.05"
          :precision="2"
          size="small"
          controls-position="right"
          @update:model-value="patchLeg(scope.$index, { riskCap: Number($event) })"
        />
      </template>
    </el-table-column>

    <el-table-column width="96" align="right">
      <template #default="scope">
        <el-button
          type="danger"
          link
          :disabled="disabled"
          :aria-label="t('basket.removeLeg')"
          @click="removeLeg(scope.$index)"
        >
          {{ t('basket.removeLeg') }}
        </el-button>
      </template>
    </el-table-column>
  </el-table>
</template>

<style scoped lang="less" src="./BasketLegTable.less"></style>
